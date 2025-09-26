#!/usr/bin/env python3
"""
Script to build the C++ extensions for ReplicantDriveSim.
This script can be used to build the pybind11 extensions manually.
"""
import subprocess
import sys
import os

def build_extensions():
    """Build the C++ extensions."""
    print("Building C++ extensions for ReplicantDriveSim...")
    
    # Check if we're in the correct directory
    if not os.path.exists("Assets/Plugins/TrafficSimulation"):
        print("Error: Please run this script from the ReplicantDriveSim root directory")
        return False
    
    try:
        # Build the extensions using setup.py
        result = subprocess.run([
            sys.executable, "setup.py", "build_ext", "--inplace"
        ], check=True, capture_output=True, text=True)
        
        print("Build successful!")
        print("STDOUT:", result.stdout)
        if result.stderr:
            print("STDERR:", result.stderr)
        
        # Check if the .so file was created
        import glob
        so_files = glob.glob("replicantdrivesim/replicantdrivesim*.so")
        if so_files:
            print(f"Extension built successfully: {so_files[0]}")
        else:
            print("Warning: No .so file found after build")
            
        return True
        
    except subprocess.CalledProcessError as e:
        print(f"Build failed with error code {e.returncode}")
        print("STDOUT:", e.stdout)
        print("STDERR:", e.stderr)
        return False
    except Exception as e:
        print(f"Unexpected error during build: {e}")
        return False

def test_import():
    """Test if the extensions can be imported."""
    print("\nTesting import...")
    try:
        import replicantdrivesim
        from replicantdrivesim import Traffic, Vehicle
        print("✓ Import successful!")
        print(f"  - replicantdrivesim version: {replicantdrivesim.__version__}")
        print("  - Traffic class imported successfully")
        print("  - Vehicle class imported successfully")
        return True
    except ImportError as e:
        print(f"✗ Import failed: {e}")
        return False

if __name__ == "__main__":
    success = build_extensions()
    if success:
        test_import()
    else:
        print("\nBuild failed. Please check the error messages above.")
        sys.exit(1)