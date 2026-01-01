# Makefile for ReplicantDriveSim
# Modern Python packaging automation and build system integration

.PHONY: help install install-prod install-dev build-native build-unity build-all test clean clean-all docs lint format check

# Default target
.DEFAULT_GOAL := help

# Python interpreter detection
PYTHON ?= $(shell which python3)
PIP := $(PYTHON) -m pip

# Build scripts
BUILD_NATIVE_SCRIPT := ./build_native_library.sh
BUILD_UNITY_SCRIPT := ./build_unity_app.sh

# Colors for help output
BOLD := \033[1m
RESET := \033[0m
GREEN := \033[32m
CYAN := \033[36m

##@ General

help: ## Display this help message
	@echo "$(BOLD)ReplicantDriveSim - Build & Installation Targets$(RESET)"
	@echo ""
	@awk 'BEGIN {FS = ":.*##"; printf "Usage: make $(CYAN)<target>$(RESET)\n\n"} \
		/^[a-zA-Z_-]+:.*?##/ { printf "  $(CYAN)%-20s$(RESET) %s\n", $$1, $$2 } \
		/^##@/ { printf "\n$(BOLD)%s$(RESET)\n", substr($$0, 5) }' $(MAKEFILE_LIST)

##@ Installation

install: ## Install package in editable mode (development)
	@echo "$(GREEN)Installing replicantdrivesim in editable mode...$(RESET)"
	@UNAME_S=$$(uname -s); UNAME_M=$$(uname -m); \
	if [ "$$UNAME_S" = "Darwin" ] && [ "$$UNAME_M" = "arm64" ]; then \
		echo "$(CYAN)Detected macOS arm64. Applying clean dependency workaround...$(RESET)"; \
		echo "$(GREEN)Step 1: Installing native dependencies via conda...$(RESET)"; \
		conda install -y -c conda-forge "grpcio=1.48.1" "protobuf=3.20.3"; \
		echo "$(GREEN)Step 2: Downgrading pip and build tools for gym 0.21.0 compatibility...$(RESET)"; \
		$(PYTHON) -m pip install 'pip<24.1' 'setuptools<66' 'wheel<0.38' 'pybind11>=2.12.0'; \
		echo "$(GREEN)Step 3: Installing core dependencies (no-deps)...$(RESET)"; \
		$(PYTHON) -m pip install mlagents==1.1.0 mlagents-envs==1.1.0 --no-deps; \
		echo "$(GREEN)Step 4: Installing gym 0.21.0 with compatible tools...$(RESET)"; \
		$(PYTHON) -m pip install gym==0.21.0 --no-build-isolation; \
		echo "$(GREEN)Step 5: Upgrading pip and build tools for main package...$(RESET)"; \
		$(PYTHON) -m pip install 'pip>=24.0' 'setuptools>=69.5.1,<70' 'wheel>=0.43.0'; \
		echo "$(GREEN)Step 6: Installing package (no build isolation, no deps)...$(RESET)"; \
		$(PYTHON) -m pip install --no-build-isolation --no-deps -e .; \
		echo "$(GREEN)Step 7: Ensuring remaining dependencies are satisfied...$(RESET)"; \
		$(PYTHON) -m pip install "gymnasium==0.28.1" "ray[rllib]==2.31.0" "numpy>=1.23.5,<1.24.0" "torch>=2.1.1" "mlflow<3.0.0" "yamale>=6.1.0" "protobuf==3.20.3"; \
	else \
		echo "$(GREEN)Step 1: Downgrading pip and build tools for gym 0.21.0 compatibility...$(RESET)"; \
		$(PYTHON) -m pip install 'pip<24.1' 'setuptools<66' 'wheel<0.38' 'pybind11>=2.12.0'; \
		echo "$(GREEN)Step 2: Installing gym 0.21.0 with compatible tools...$(RESET)"; \
		$(PYTHON) -m pip install gym==0.21.0 --no-build-isolation; \
		echo "$(GREEN)Step 3: Upgrading pip and build tools for main package...$(RESET)"; \
		$(PYTHON) -m pip install 'pip>=24.0' 'setuptools>=69.5.1,<70' 'wheel>=0.43.0'; \
		echo "$(GREEN)Step 4: Installing package (no build isolation)...$(RESET)"; \
		$(PYTHON) -m pip install --no-build-isolation -e .; \
	fi

install-prod: ## Install package (production, non-editable)
	@echo "$(GREEN)Installing replicantdrivesim...$(RESET)"
	$(PIP) install .

install-dev: ## Install package with development dependencies
	@echo "$(GREEN)Installing replicantdrivesim with dev dependencies...$(RESET)"
	$(PIP) install -e ".[dev]"

install-all: ## Install package with all optional dependencies
	@echo "$(GREEN)Installing replicantdrivesim with all dependencies...$(RESET)"
	$(PIP) install -e ".[all]"

uninstall: ## Uninstall package
	@echo "$(GREEN)Uninstalling replicantdrivesim...$(RESET)"
	$(PIP) uninstall -y replicantdrivesim

##@ Build

build-native: ## Build C++ native library (traffic simulation plugin)
	@echo "$(GREEN)Building native library...$(RESET)"
	@if [ ! -f "$(BUILD_NATIVE_SCRIPT)" ]; then \
		echo "Error: $(BUILD_NATIVE_SCRIPT) not found"; \
		exit 1; \
	fi
	@chmod +x $(BUILD_NATIVE_SCRIPT)
	$(BUILD_NATIVE_SCRIPT) $(PYTHON)

build-unity: ## Build Unity application (standalone builds)
	@echo "$(GREEN)Building Unity application...$(RESET)"
	@if [ ! -f "$(BUILD_UNITY_SCRIPT)" ]; then \
		echo "Error: $(BUILD_UNITY_SCRIPT) not found"; \
		exit 1; \
	fi
	@chmod +x $(BUILD_UNITY_SCRIPT)
	$(BUILD_UNITY_SCRIPT)

build-all: build-native build-unity ## Build both native library and Unity application
	@echo "$(GREEN)All builds completed successfully!$(RESET)"

rebuild-native: clean-native build-native ## Clean and rebuild native library

rebuild-unity: clean-unity build-unity ## Clean and rebuild Unity application

rebuild-all: clean-all build-all ## Clean and rebuild everything

##@ Testing

test: ## Run pytest test suite
	@echo "$(GREEN)Running tests...$(RESET)"
	$(PYTHON) -m pytest

test-cov: ## Run tests with coverage report
	@echo "$(GREEN)Running tests with coverage...$(RESET)"
	$(PYTHON) -m pytest --cov=replicantdrivesim --cov-report=html --cov-report=term-missing

test-verbose: ## Run tests in verbose mode
	@echo "$(GREEN)Running tests (verbose)...$(RESET)"
	$(PYTHON) -m pytest -vv

##@ Code Quality

lint: ## Run linting checks (flake8)
	@echo "$(GREEN)Running linter...$(RESET)"
	$(PYTHON) -m flake8 replicantdrivesim tests

format: ## Format code with black and isort
	@echo "$(GREEN)Formatting code...$(RESET)"
	$(PYTHON) -m black replicantdrivesim tests
	$(PYTHON) -m isort replicantdrivesim tests

format-check: ## Check code formatting without making changes
	@echo "$(GREEN)Checking code formatting...$(RESET)"
	$(PYTHON) -m black --check replicantdrivesim tests
	$(PYTHON) -m isort --check-only replicantdrivesim tests

type-check: ## Run mypy type checking
	@echo "$(GREEN)Running type checker...$(RESET)"
	$(PYTHON) -m mypy replicantdrivesim

check: format-check lint type-check ## Run all code quality checks

##@ Documentation

docs: ## Build documentation with Sphinx
	@echo "$(GREEN)Building documentation...$(RESET)"
	@if [ -d "docs" ]; then \
		cd docs && make html; \
	else \
		echo "Warning: docs directory not found"; \
	fi

docs-serve: docs ## Build and serve documentation locally
	@echo "$(GREEN)Serving documentation at http://localhost:8000$(RESET)"
	@cd docs/_build/html && $(PYTHON) -m http.server 8000

##@ Cleanup

clean: ## Remove Python build artifacts
	@echo "$(GREEN)Cleaning Python build artifacts...$(RESET)"
	rm -rf build/
	rm -rf dist/
	rm -rf *.egg-info
	rm -rf .eggs/
	find . -type d -name __pycache__ -exec rm -rf {} + 2>/dev/null || true
	find . -type f -name "*.pyc" -delete
	find . -type f -name "*.pyo" -delete
	find . -type f -name "*~" -delete

clean-native: ## Remove native library build artifacts
	@echo "$(GREEN)Cleaning native library build artifacts...$(RESET)"
	rm -rf Assets/Plugins/TrafficSimulation/build/
	find Assets/Plugins/TrafficSimulation -type f \( -name "*.so" -o -name "*.dylib" -o -name "*.dll" \) -delete

clean-unity: ## Remove Unity build artifacts
	@echo "$(GREEN)Cleaning Unity build artifacts...$(RESET)"
	rm -rf Builds/StandaloneOSX/
	rm -rf Builds/StandaloneLinux64/
	rm -rf Builds/StandaloneWindows64/

clean-test: ## Remove test and coverage artifacts
	@echo "$(GREEN)Cleaning test artifacts...$(RESET)"
	rm -rf .pytest_cache/
	rm -rf htmlcov/
	rm -rf .coverage
	rm -rf coverage.xml

clean-docs: ## Remove documentation build artifacts
	@echo "$(GREEN)Cleaning documentation artifacts...$(RESET)"
	@if [ -d "docs/_build" ]; then \
		rm -rf docs/_build/; \
	fi

clean-all: clean clean-native clean-unity clean-test clean-docs ## Remove all build artifacts
	@echo "$(GREEN)All artifacts cleaned!$(RESET)"

##@ Development

dev-setup: install-dev ## Complete development environment setup
	@echo "$(GREEN)Development environment setup complete!$(RESET)"
	@echo "You can now:"
	@echo "  - Run tests: make test"
	@echo "  - Format code: make format"
	@echo "  - Build native library: make build-native"
	@echo "  - Build Unity app: make build-unity"

conda-env: ## Create conda environment from environment.yml
	@echo "$(GREEN)Creating conda environment...$(RESET)"
	conda env create -f environment.yml

conda-update: ## Update conda environment from environment.yml
	@echo "$(GREEN)Updating conda environment...$(RESET)"
	conda env update -f environment.yml --prune

##@ CI/CD

ci-test: ## Run tests in CI mode (with coverage XML output)
	@echo "$(GREEN)Running CI tests...$(RESET)"
	$(PYTHON) -m pytest --cov=replicantdrivesim --cov-report=xml --cov-report=term

ci-build: build-all ## CI build target (builds everything)
	@echo "$(GREEN)CI build completed!$(RESET)"

##@ Package Management

dist: clean ## Build source and wheel distributions
	@echo "$(GREEN)Building distributions...$(RESET)"
	$(PYTHON) -m build

upload-test: dist ## Upload package to TestPyPI
	@echo "$(GREEN)Uploading to TestPyPI...$(RESET)"
	$(PYTHON) -m twine upload --repository testpypi dist/*

upload: dist ## Upload package to PyPI
	@echo "$(GREEN)Uploading to PyPI...$(RESET)"
	$(PYTHON) -m twine upload dist/*

##@ Information

show-deps: ## Show installed dependencies
	@echo "$(GREEN)Installed dependencies:$(RESET)"
	$(PIP) list

show-version: ## Show package version
	@echo "$(GREEN)Package version:$(RESET)"
	@$(PYTHON) -c "import tomllib; print(tomllib.load(open('pyproject.toml', 'rb'))['project']['version'])" 2>/dev/null || \
	 $(PYTHON) -c "import tomli; print(tomli.load(open('pyproject.toml', 'rb'))['project']['version'])" 2>/dev/null || \
	 grep "version = " pyproject.toml | head -1 | cut -d'"' -f2

show-info: ## Show package information
	@echo "$(GREEN)Package information:$(RESET)"
	$(PIP) show replicantdrivesim || echo "Package not installed. Run 'make install' first."
