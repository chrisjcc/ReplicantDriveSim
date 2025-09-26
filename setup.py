from setuptools import setup, find_packages, Extension
import os

# Check if we can import pybind11
try:
    from pybind11.setup_helpers import Pybind11Extension, build_ext
    from pybind11 import get_include as pybind11_get_include
    
    # Define the extension module
    ext_modules = [
        Pybind11Extension(
            "replicantdrivesim.replicantdrivesim",
            sorted([
                "Assets/Plugins/TrafficSimulation/src/simulation.cpp",
                "Assets/Plugins/TrafficSimulation/src/traffic.cpp", 
                "Assets/Plugins/TrafficSimulation/src/vehicle.cpp",
                "Assets/Plugins/TrafficSimulation/src/bindings.cpp",
                "Assets/Plugins/TrafficSimulation/src/bicycle_model.cpp",
            ]),
            include_dirs=[
                "Assets/Plugins/TrafficSimulation/include",
                pybind11_get_include(),
            ],
            language='c++',
            cxx_std=14,
        ),
    ]
    
    cmdclass = {"build_ext": build_ext}
    
except ImportError:
    print("Warning: pybind11 not found. C++ extensions will not be built.")
    print("To build C++ extensions, install pybind11:")
    print("    pip install pybind11")
    ext_modules = []
    cmdclass = {}

setup(
    packages=find_packages(exclude=['tests']),
    include_package_data=True,
    zip_safe=False,
    ext_modules=ext_modules,
    cmdclass=cmdclass,
    python_requires=">=3.8",
    install_requires=[
        # Add pybind11 as a requirement if we have extensions
    ] + (["pybind11"] if ext_modules else []),
)
