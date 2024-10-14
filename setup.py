import glob
import os
import pathlib
import subprocess
import sys

from setuptools import Extension, setup
from setuptools.command.build_ext import build_ext


class CMakeExtension(Extension):
    def __init__(self, name, sourcedir=""):
        Extension.__init__(self, name, sources=[])
        self.sourcedir = os.path.abspath(sourcedir)


class CMakeBuild(build_ext):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        # Initialize file_extension with a default value
        self.file_extension = ".so"  # Default value for non-macOS platforms

    def run(self):
        # Set the MACOSX_DEPLOYMENT_TARGET environment variable
        if sys.platform == "darwin":
            os.environ["MACOSX_DEPLOYMENT_TARGET"] = "14.0"

        # Check if CMake is installed
        try:
            subprocess.check_output(["cmake", "--version"])
        except OSError:
            raise RuntimeError(
                "CMake must be installed to build the following extensions: "
                + ", ".join(e.name for e in self.extensions)
            )

        for ext in self.extensions:
            self.build_extension(ext)

    def build_extension(self, ext):
        env = os.environ.copy()
        sourcedir = ext.sourcedir
        build_temp = os.path.abspath(self.build_temp)
        if not os.path.exists(build_temp):
            os.makedirs(build_temp)

        cmake_args = [
            "-DCMAKE_LIBRARY_OUTPUT_DIRECTORY=" + build_temp,
            "-DCMAKE_BUILD_TYPE=" + ("Debug" if self.debug else "Release"),
            "-DVERSION_INFO={}".format(self.distribution.get_version()),
        ]

        # Change to build directory
        old_cwd = os.getcwd()
        os.chdir(build_temp)
        try:
            subprocess.check_call(["cmake", str(sourcedir)] + cmake_args, env=env)
            subprocess.check_call(["cmake", "--build", ".", "--", "-j4"])
        finally:
            os.chdir(old_cwd)

        # Move the built library (produced by CMake) to the correct location
        lib_paths = glob.glob(
            os.path.join(
                build_temp,
                f"replicantdrivesim_pypi{self.file_extension}"
            )
        )

        if lib_paths:  # Check if the glob result is not empty
            lib_path = lib_paths[0]  # Take the first matching file
        else:
            raise FileNotFoundError(f"No files found for pattern {lib_paths}")

        dest_path = os.path.dirname(self.get_ext_fullpath(ext.name))
        dest_file = self.get_ext_fullpath(ext.name)

        if not os.path.exists(dest_path):
            os.makedirs(dest_path)

        if os.path.exists(dest_file):
            os.remove(dest_file)  # Avoid permission issues

        # Now move and rename the CMake-built library (.dylib or .so) to the correct destination and name
        self.move_file(lib_path, dest_file)


# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

# Source directory
sourcedir = os.environ.get("TRAFFIC_SIM_SOURCEDIR", "/app/repo/External")

setup(
    name="ReplicantDriveSim",
    version="0.1.9",
    author="Christian Contreras Campana",
    author_email="chrisjcc.physics@gmail.com",
    description="Traffic simulation package with C++ backend",
    long_description=long_description,
    long_description_content_type="text/markdown",
    # Name should match the module name defined in the C++ code, i.e. pybind11PYBIND11_MODULE(name, m)
    ext_modules=[CMakeExtension(name="replicantdrivesim", sourcedir=sourcedir)],
    cmdclass={"build_ext": CMakeBuild},
    zip_safe=False,
    python_requires=">=3.6",
    package_data={"": ["*.so", "*.dylib"]},
    url="https://chrisjcc.github.io/ReplicantDriveSim/",
    license="MIT",
    # install_requires=[  # Add required dependencies as needed
    #    'numpy',
    #    'torch',
    #    'rllib'
    # ],
)
