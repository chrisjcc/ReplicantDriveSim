"""
Modern PEP 517/518/621 setup stub.

All package configuration is in pyproject.toml following modern Python packaging standards.
This file exists only for backward compatibility with legacy tooling.

For installation:
    pip install .              # Regular install
    pip install -e .           # Editable/development install
    pip install -e .[dev]      # With development dependencies
    pip install -e .[all]      # With all optional dependencies

Or use the Makefile:
    make install               # Editable install
    make install-dev           # With dev dependencies
    make install-all           # With all dependencies
"""

from setuptools import setup

# All configuration is in pyproject.toml
setup()
