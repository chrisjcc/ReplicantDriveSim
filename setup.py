import os
import pathlib
from setuptools import setup, find_packages

def package_files(directory):
    """Recursively gather all files in a directory."""
    paths = []
    for (path, directories, filenames) in os.walk(directory):
        for filename in filenames:
            paths.append(os.path.join('..', path, filename))
    return paths

# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

# Find all unity related files
unity_executable_files = package_files(os.path.join("replicantdrivesim", "Builds", "StandaloneOSX"))

setup(
    name="ReplicantDriveSim",
    version="0.2.6",
    author="Christian Contreras Campana",
    author_email="chrisjcc.physics@gmail.com",
    description="Unity Traffic Simulation",
    long_description=long_description,
    long_description_content_type="text/markdown",
    packages=find_packages(exclude=['tests']),
    package_data={
        "replicantdrivesim": ["*.so"],
        "": ["configs/*.yaml"] + unity_executable_files,
    },
    include_package_data=True,
    zip_safe=False,
    python_requires=">=3.6",
    url="https://chrisjcc.github.io/ReplicantDriveSim/",
    license="MIT",
    # Uncomment and update as needed
    # install_requires=[
    #     'numpy',
    #     'pyyaml',
    #     'gymnasium',
    #     'ray',
    #     'rllib'
    # ],
)
