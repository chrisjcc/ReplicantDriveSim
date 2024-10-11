import os
import sys
import glob
import pathlib
import subprocess
from setuptools import setup, find_packages, Extension
from setuptools.command.build_ext import build_ext

def get_git_root():
    try:
        # Run 'git rev-parse --show-toplevel' to get the root directory of the git repo
        git_root = subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], 
                                           universal_newlines=True).strip()
        return pathlib.Path(git_root)
    except subprocess.CalledProcessError:
        # Handle case where the script is not inside a Git repository
        return pathlib.Path(__file__).parent.resolve()


class CMakeExtension(Extension):
    def __init__(self, name, sourcedir=''):
        Extension.__init__(self, name, sources=[])
        self.sourcedir = os.path.abspath(sourcedir)

class CMakeBuild(build_ext):
    def run(self):
        try:
            out = subprocess.check_output(['cmake', '--version'])
        except OSError:
            raise RuntimeError(
                "CMake must be installed to build the following extensions: " +
                ", ".join(e.name for e in self.extensions))

        for ext in self.extensions:
            self.build_extension(ext)

    def build_extension(self, ext):
        extdir = os.path.abspath(os.path.dirname(self.get_ext_fullpath(ext.name)))
        cmake_args = [
            #f'-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={extdir}',
            f'-DCMAKE_LIBRARY_OUTPUT_DIRECTORY={os.path.join(ext.sourcedir, "Assets", "Plugins", "TrafficSimulation", "build")}'
            f'-DPYTHON_EXECUTABLE={sys.executable}',
            f'-DCMAKE_BUILD_TYPE={"Debug" if self.debug else "Release"}'
        ]

        build_temp = os.path.join(self.build_temp, ext.name)
        if not os.path.exists(build_temp):
            os.makedirs(build_temp)


        subprocess.check_call(['cmake', ext.sourcedir] + cmake_args, cwd=build_temp)
        subprocess.check_call(['cmake', '--build', '.'], cwd=build_temp)

        print(f"Build temp directory: {build_temp}")
        print(f"Extension directory: {extdir}")

        # Look for the built library in the correct location
        #lib_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Assets', 'Plugins', 'TrafficSimulation', 'build')
        lib_dir = os.path.join('/Users/christiancontrerascampana/Desktop/project/unity_traffic_simulation/reduce_git_lfs/production_ready/ReplicantDriveSim/Assets/Plugins/TrafficSimulation/build')


        #lib_file = os.path.join(lib_dir, f"lib{ext.name}.so")
        #lib_file = os.path.join(lib_dir, "ReplicantDriveSim.cpython-38-darwin.so")
        lib_file = os.path.join(lib_dir, "libReplicantDriveSim.so")

        if not os.path.exists(lib_file):
            lib_file = os.path.join(lib_dir, f"lib{ext.name}.dylib")

        if not os.path.exists(lib_file):
            raise RuntimeError(f"Could not find the built library in {lib_file}")

        print(f"Found library: {lib_file}")

        # Copy the library file to the correct location
        #target_file = "build/ReplicantDriveSim.cpython-38-darwin.so" #self.get_ext_fullpath(ext.name)
        target_file = "build/libReplicantDriveSim.so"
        self.copy_file(lib_file, target_file)


# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()

# Now we get the root directory of the Git repository
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

sourcedir = os.environ.get('TRAFFIC_SIM_SOURCEDIR', '/app/repo/External')

setup(
    name='ReplicantDriveSim',
    version='0.1.3',
    packages=find_packages(),
    author='Christian Contreras Campana',
    author_email='chrisjcc.physics@gmail.com',
    description='Traffic simulation package with C++ backend',
    long_description=long_description,  # Adding long description
    long_description_content_type='text/markdown',  # Tells PyPI it's markdown
    ext_modules=[CMakeExtension('ReplicantDriveSim', sourcedir=sourcedir)],
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
