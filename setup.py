import os
import sys
import glob
import pathlib
import subprocess
from setuptools import setup, Extension
from setuptools.command.build_ext import build_ext

class CMakeExtension(Extension):
    def __init__(self, name, sourcedir=''):
        Extension.__init__(self, name, sources=[])
        self.sourcedir = os.path.abspath(sourcedir)

class CMakeBuild(build_ext):
    #def get_ext_filename(self, ext_name):
    #    # Override the default extension
    #    filename = super().get_ext_filename(ext_name)
    #    if sys.platform == "darwin":
    #        # On macOS, use .dylib instead of .so
    #        return filename.replace(".so", ".dylib")
    #    return filename

    def run(self):
        # Set the MACOSX_DEPLOYMENT_TARGET environment variable
        if sys.platform == "darwin":
            os.environ["MACOSX_DEPLOYMENT_TARGET"] = "14.0"
            # Use the correct file extension based on the platform
            #self.file_extension = ".so" #".dylib"
        #else:
        #    self.file_extension = ".so"

        # Check if CMake is installed
        try:
            subprocess.check_output(['cmake', '--version'])
        except OSError:
            raise RuntimeError("CMake must be installed to build the following extensions: " +
                               ", ".join(e.name for e in self.extensions))

        for ext in self.extensions:
            self.build_extension(ext)

    def build_extension(self, ext):
        sourcedir = ext.sourcedir
        build_temp = os.path.abspath(self.build_temp)
        if not os.path.exists(build_temp):
            os.makedirs(build_temp)

        # Get the pybind11 directory dynamically
        pybind11_dir = subprocess.check_output(['python3', '-m', 'pybind11', '--cmakedir']).decode().strip()

        cmake_args = [
            '-DCMAKE_LIBRARY_OUTPUT_DIRECTORY=' + os.path.join(build_temp, 'lib'),
            '-DPYTHON_EXECUTABLE=' + sys.executable,
            '-Dpybind11_DIR=' + pybind11_dir,
            '-DCMAKE_BUILD_TYPE=' + ('Debug' if self.debug else 'Release'),
            '-DVERSION_INFO={}'.format(self.distribution.get_version()),
        ]

        build_args = ['--', '-j4']

        env = os.environ.copy()

        # Change to build directory
        old_cwd = os.getcwd()
        os.chdir(build_temp)
        try:
            subprocess.check_call(['cmake', str(sourcedir)] + cmake_args, env=env)
            subprocess.check_call(['cmake', '--build', '.'] + build_args)
        finally:
            os.chdir(old_cwd)

        # Move the built library to the proper location
        print(glob.glob(os.path.join(build_temp, 'lib', '*')))
        lib_path = glob.glob(os.path.join(build_temp, 'lib', 'simulation*.so'))[0] # TODO: .so
        #lib_path = glob.glob(os.path.join(build_temp, 'lib', f'simulation*{self.file_extension}'))[0] # TODO: .so
        dest_path = os.path.dirname(self.get_ext_fullpath(ext.name))

        if not os.path.exists(dest_path):
            os.makedirs(dest_path)

        dest_file = self.get_ext_fullpath(ext.name)
        if os.path.exists(dest_file):
            os.remove(dest_file)  # Avoid permission issues

        self.move_file(lib_path, dest_file)

# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

# Source directory
sourcedir = os.environ.get('TRAFFIC_SIM_SOURCEDIR', '/app/repo/External')

setup(
    name='ReplicantDriveSim',
    version='0.1.8',
    author='Christian Contreras Campana',
    author_email='chrisjcc.physics@gmail.com',
    description='Traffic simulation package with C++ backend',
    long_description=long_description,
    long_description_content_type='text/markdown',
    ext_modules=[CMakeExtension('simulation', sourcedir=sourcedir)],
    cmdclass={'build_ext': CMakeBuild},
    zip_safe=False,
    python_requires='>=3.6',
    package_data={'': ['*.so', '*.dylib']},
    url='https://chrisjcc.github.io/ReplicantDriveSim/',
    license='MIT',
    #install_requires=[  # Add required dependencies as needed
    #    'numpy', 
    #    'torch', 
    #    'rllib'
    #],
)
