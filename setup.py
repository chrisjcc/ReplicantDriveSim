import os
import sys
import glob
import shutil
import pathlib
import subprocess
from setuptools import setup, Extension
from setuptools.command.build_ext import build_ext

class CMakeExtension(Extension):
    def __init__(self, name, sourcedir=''):
        Extension.__init__(self, name, sources=[])
        self.sourcedir = os.path.abspath(sourcedir)

class CMakeBuild(build_ext):
    def run(self):
        try:
            out = subprocess.check_output(['cmake', '--version'])
        except OSError:
            raise RuntimeError("CMake must be installed to build the following extensions: " +
                               ", ".join(e.name for e in self.extensions))

        for ext in self.extensions:
            self.build_extension(ext)

    def build_extension(self, ext):
        extdir = os.path.abspath(os.path.dirname(self.get_ext_fullpath(ext.name)))
        print("TODO: extdir: ", extdir)
        print("TODO: build_temp: ", self.build_temp)
        print("TODO: ext.name: ", ext.name)
        build_temp = os.path.join(self.build_temp, ext.name)
        print("TODO: build_temp: ", self.build_temp)

        if not os.path.exists(build_temp):
            os.makedirs(build_temp)

        # Get the pybind11 directory dynamically
        #pybind11_dir = subprocess.check_output(['python3', '-m', 'pybind11', '--cmakedir']).decode().strip()

        # ADD: MACOSX_DEPLOYMENT_TARGET=14
        cmake_args = [
                f'-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={extdir}',
                #f'-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={build_temp}',
                '-DPYTHON_EXECUTABLE=' + sys.executable,
                #'-Dpybind11_DIR=' + pybind11_dir,
                '-DCMAKE_BUILD_TYPE=' + ('Debug' if self.debug else 'Release'),
                '-DVERSION_INFO={}'.format(self.distribution.get_version()), # Pass VERSION_INFO directly
        ]

        subprocess.check_call(['cmake', ext.sourcedir] + cmake_args, cwd=build_temp)
        subprocess.check_call(['cmake', '--build', '.'], cwd=build_temp)

        # Copy the built library to the correct location
        print("TODO: GLOB: ", glob.glob(os.path.join(extdir, '*')))

        built_lib = glob.glob(os.path.join(extdir, 'lib*simulation*.so')) or \
        glob.glob(os.path.join(extdir, 'lib*simulation*.dylib'))
        
        if not built_lib:
            raise RuntimeError("Built library not found")
        
        built_lib = built_lib[0]

        dest_path = self.get_ext_fullpath(ext.name)
        os.makedirs(os.path.dirname(dest_path), exist_ok=True)
        print(f"Copied {built_lib} to {dest_path}")
        shutil.copy(built_lib, dest_path)


# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()

# Now we get the root directory of the Git repository
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

# Source directory
sourcedir = os.environ.get('TRAFFIC_SIM_SOURCEDIR', '/app/repo/External')

setup(
    name='simulation',
    version='0.1.8',
    author='Christian Contreras Campana',
    author_email='chrisjcc.physics@gmail.com',
    description='Traffic simulation package with C++ backend',
    long_description=long_description,  # Adding long description
    long_description_content_type='text/markdown',  # Tells PyPI it's markdown
    ext_modules=[CMakeExtension(
        'simulation',
        sourcedir=sourcedir
    )],
    cmdclass={'build_ext': CMakeBuild},
    zip_safe=False,
    python_requires='>=3.6',
    package_data={
        '': ['*.so', '*.dylib'],
    },
    url='https://chrisjcc.github.io/ReplicantDriveSim/',  # Home-page
    license='MIT',  # License type (update accordingly)
    #install_requires=[  # Required dependencies
    #    'numpy',        # Example dependency, add others as needed
    #    'torch',        # Example dependency
    #    'rllib'
    #],
)
