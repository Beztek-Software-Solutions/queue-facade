# Copyright (c) Beztek Software Solutions. All rights reserved.

# Queue Facade — unit tests (+ optional live via QUEUEFACADE_LIVE_PROVIDERS).
#
#   make test
#   make test-unit
#   make test-live   # requires QUEUEFACADE_LIVE_PROVIDERS
#   make coverage
#   make coverage-html

SHELL := /bin/bash
.SHELLFLAGS := -euo pipefail -c

SOLUTION := queue-facade.sln
TESTS_PROJECT := QueueFacade.Tests/Beztek.Facade.Queue.Tests.csproj
ASSEMBLY_INCLUDE := [Beztek.Facade.Queue]*

COVERAGE_DIR := $(CURDIR)/coverage
COVERAGE_THRESHOLD ?= 0
OPEN_CMD ?= open

# Serialize restore/build/test against concurrent make targets sharing bin/.
DOTNET_LOCK := $(CURDIR)/.dotnet-build.lock

# Unit-only by default (no Testcontainers). Override: COVERAGE_FILTER=
COVERAGE_FILTER ?= Category!=Live

.DEFAULT_GOAL := help

.PHONY: help
help:
	@echo "Queue Facade"
	@echo "  make test              - unit tests (exclude Category=Live)"
	@echo "  make test-unit         - same as test"
	@echo "  make test-live         - Category=Live (needs QUEUEFACADE_LIVE_PROVIDERS)"
	@echo "  make coverage          - Coverlet + terminal summary (unit by default)"
	@echo "  make coverage-html     - HTML report opened in a browser"
	@echo "  make coverage-check    - fail if line coverage < COVERAGE_THRESHOLD (default 0→85)"
	@echo "  make clean             - drop build output and coverage/"
	@echo "  make tools             - restore local dotnet tools (reportgenerator)"
	@echo ""
	@echo "Coverage overrides: COVERAGE_FILTER=  COVERAGE_THRESHOLD=85"
	@echo "Note: build/test/coverage share $(notdir $(DOTNET_LOCK)) so parallel make is safe."

.PHONY: tools
tools:
	dotnet tool restore

.PHONY: restore
restore:
	@flock --close "$(DOTNET_LOCK)" dotnet restore $(SOLUTION)

.PHONY: build
build:
	@flock --close "$(DOTNET_LOCK)" bash -c 'dotnet restore $(SOLUTION) && dotnet build $(SOLUTION) --no-restore'

.PHONY: test test-unit
test test-unit:
	@flock --close "$(DOTNET_LOCK)" bash -c '\
		dotnet restore $(SOLUTION) && \
		dotnet build $(SOLUTION) --no-restore && \
		dotnet test $(TESTS_PROJECT) --no-build --filter "Category!=Live"'

.PHONY: test-live
test-live:
	@test -n "$${QUEUEFACADE_LIVE_PROVIDERS:-}" || { echo "Set QUEUEFACADE_LIVE_PROVIDERS (e.g. localmemory,azure,sqs,all)"; exit 1; }
	@flock --close "$(DOTNET_LOCK)" bash -c '\
		dotnet restore $(SOLUTION) && \
		dotnet build $(SOLUTION) --no-restore && \
		dotnet test $(TESTS_PROJECT) --no-build --filter "Category=Live"'

.PHONY: coverage
coverage: tools
	@rm -rf "$(COVERAGE_DIR)"
	@mkdir -p "$(COVERAGE_DIR)"
	@flock --close "$(DOTNET_LOCK)" bash -c '\
		dotnet test $(TESTS_PROJECT) \
			$(if $(COVERAGE_FILTER),--filter "$(COVERAGE_FILTER)",) \
			/p:CollectCoverage=true \
			/p:CoverletOutputFormat=cobertura \
			/p:CoverletOutput="$(COVERAGE_DIR)/" \
			/p:Include='\''$(ASSEMBLY_INCLUDE)'\'' \
			/p:Threshold=$(COVERAGE_THRESHOLD) \
			/p:ThresholdType=line'
	@dotnet reportgenerator \
		"-reports:$(COVERAGE_DIR)/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/report" \
		-reporttypes:TextSummary \
		-verbosity:Warning
	@cat "$(COVERAGE_DIR)/report/Summary.txt"

.PHONY: coverage-html
coverage-html: coverage
	@dotnet reportgenerator \
		"-reports:$(COVERAGE_DIR)/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/html" \
		-reporttypes:Html \
		-verbosity:Warning
	@echo "HTML report: $(COVERAGE_DIR)/html/index.html"
	@# Never block make on a browser: open(1)/xdg-open can hang in headless or when a prior
	@# flock holder (e.g. long live suite) is unrelated — browser launch is best-effort only.
	@if [ -n "$${DISPLAY:-}$${WAYLAND_DISPLAY:-}" ]; then \
		( timeout 3 $(OPEN_CMD) "$(COVERAGE_DIR)/html/index.html" \
			|| timeout 3 xdg-open "$(COVERAGE_DIR)/html/index.html" \
			|| true ) >/dev/null 2>&1 & \
	fi

.PHONY: coverage-check
coverage-check: coverage
	@line=$$(sed -n 's/^[[:space:]]*Line coverage:[[:space:]]*\([0-9.]*\)%.*/\1/p' "$(COVERAGE_DIR)/report/Summary.txt" | head -1); \
	want="$(COVERAGE_THRESHOLD)"; \
	if [ -z "$$line" ]; then echo "could not parse line coverage from Summary.txt"; exit 1; fi; \
	if [ "$$want" = "0" ]; then want=85; fi; \
	awk -v got="$$line" -v w="$$want" 'BEGIN { \
		if (got+0 < w+0) { printf "FAIL: line coverage %.1f%% < %s%%\n", got, w; exit 1 } \
		printf "OK: line coverage %.1f%% >= %s%%\n", got, w; exit 0 }'

.PHONY: clean
clean:
	dotnet clean $(SOLUTION)
	rm -rf "$(COVERAGE_DIR)" "$(DOTNET_LOCK)"
