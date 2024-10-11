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
    def run(self):
        try:
            sourcedir = self.extensions[0].sourcedir
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
            ]

            build_args = ['--', '-j4']

            env = os.environ.copy()
            env['CXXFLAGS'] = '{} -DVERSION_INFO=\\"{}\\"'.format(env.get('CXXFLAGS', ''), self.distribution.get_version())

            # Change to build directory
            old_cwd = os.getcwd()
            os.chdir(build_temp)
            try:
                subprocess.check_call(['cmake', str(sourcedir)] + cmake_args, env=env)
                subprocess.check_call(['cmake', '--build', '.'] + build_args)
            finally:
                # Restore the original working directory
                os.chdir(old_cwd)
        except OSError as e:
            print('Unable to build extension: {}'.format(e))
            raise e

        # Move the built library to the proper location
        # Flexible for simulation.cpython-39-darwin.so or simulation.cpython-39-aarch64-linux-gnu.so
        # Use glob in order to expand out the use of a wildcard "*" and extract the actual name of the file
        lib_path = glob.glob(os.path.join(build_temp, 'lib', 'simulation*.so'))[0]

        # Ensure destination directory exists
        dest_path = os.path.dirname(self.get_ext_fullpath('simulation'))
        if not os.path.exists(dest_path):
            os.makedirs(dest_path)

        # Correct destination filename
        dest_file = self.get_ext_fullpath('simulation')
        if os.path.exists(dest_file):
            os.remove(dest_file)  # Remove if it already exists to avoid permission issues

        self.move_file(lib_path, dest_file)

# Reading long description from README.md
directory_path = pathlib.Path(__file__).parent.resolve()

# Now we get the root directory of the Git repository
long_description = (directory_path / "README.md").read_text(encoding="utf-8")

sourcedir = os.environ.get('TRAFFIC_SIM_SOURCEDIR', '/app/repo/External')

setup(
    name='ReplicantDriveSim',
    version='0.1.5',
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
        '': ['*.so'],
    },
    url='https://chrisjcc.github.io/ReplicantDriveSim/',  # Home-page
    license='MIT',  # License type (update accordingly)
    #install_requires=[  # Required dependencies
    #    'numpy',        # Example dependency, add others as needed
    #    'torch',        # Example dependency
    #    'rllib'
    #],
)
