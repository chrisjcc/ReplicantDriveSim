#!/usr/bin/env python3
"""
Script to check the ReplicantDriveSim environment setup.
This helps diagnose common issues before running trainer.py.
"""
import os
import sys
import platform
import subprocess
import importlib.util

def check_python_version():
    """Check Python version."""
    print("=== Python Version ===")
    print(f"Python version: {sys.version}")
    required_version = (3, 8)
    current_version = sys.version_info[:2]
    if current_version >= required_version:
        print("✓ Python version is compatible")
        return True
    else:
        print(f"✗ Python {required_version[0]}.{required_version[1]}+ required, got {current_version[0]}.{current_version[1]}")
        return False

def check_dependencies():
    """Check if required dependencies are installed."""
    print("\n=== Dependencies ===")
    dependencies = [
        'pybind11',
        'ray',
        'mlagents_envs',
        'gymnasium', 
        'yaml',
        'numpy',
        'mlflow'
    ]
    
    all_good = True
    for dep in dependencies:
        try:
            importlib.import_module(dep)
            print(f"✓ {dep}")
        except ImportError:
            print(f"✗ {dep} - not installed")
            all_good = False
    
    return all_good

def check_cpp_bindings():
    """Check if C++ bindings are available."""
    print("\n=== C++ Bindings ===")
    try:
        import replicantdrivesim
        if replicantdrivesim.cpp_bindings_available():
            print("✓ C++ bindings are available")
            return True
        else:
            print("✗ C++ bindings are not available")
            return False
    except Exception as e:
        print(f"✗ Error checking C++ bindings: {e}")
        return False

def check_unity_executable():
    """Check Unity executable availability."""
    print("\n=== Unity Executable ===")
    try:
        import replicantdrivesim
        unity_path = replicantdrivesim.get_unity_executable_path()
        print(f"Unity path: {unity_path}")
        
        if os.path.exists(unity_path):
            if os.path.isdir(unity_path):
                print("✓ Unity executable directory exists")
                
                # Check permissions on macOS
                if platform.system() == "Darwin":
                    # Check if it's executable
                    if os.access(unity_path, os.X_OK):
                        print("✓ Unity app has execute permissions")
                    else:
                        print("✗ Unity app lacks execute permissions")
                        print("  Try: chmod +x " + unity_path)
                    
                    # Check if it's a proper .app bundle
                    if unity_path.endswith('.app'):
                        contents_path = os.path.join(unity_path, 'Contents')
                        if os.path.exists(contents_path):
                            print("✓ Unity app bundle structure is correct")
                        else:
                            print("✗ Unity app bundle structure is incomplete")
                
                return True
            else:
                print("✓ Unity executable file exists")
                return True
        else:
            print("✗ Unity executable not found")
            return False
    except Exception as e:
        print(f"✗ Error checking Unity executable: {e}")
        return False

def check_config_files():
    """Check if required config files exist."""
    print("\n=== Configuration Files ===")
    config_files = [
        "examples/configs/config.yaml",
        "replicantdrivesim/configs/config_schema.yaml"
    ]
    
    all_good = True
    for config_file in config_files:
        if os.path.exists(config_file):
            print(f"✓ {config_file}")
        else:
            print(f"✗ {config_file} - missing")
            all_good = False
    
    return all_good

def suggest_fixes():
    """Suggest common fixes."""
    print("\n=== Suggested Fixes ===")
    
    print("1. Install missing dependencies:")
    print("   pip install pybind11 ray[rllib] mlagents_envs gymnasium pyyaml mlflow")
    
    print("\n2. Build C++ extensions:")
    print("   python setup.py build_ext --inplace")
    print("   # OR")
    print("   pip install -e . --force-reinstall")
    
    print("\n3. For macOS Unity app permissions:")
    print("   chmod +x replicantdrivesim/Builds/StandaloneOSX/libReplicantDriveSim.app")
    
    print("\n4. Check Ray worker environment:")
    print("   Make sure all dependencies are installed in the same environment where Ray workers run")

def main():
    """Main function to run all checks."""
    print("ReplicantDriveSim Environment Check")
    print("=" * 50)
    
    checks = [
        check_python_version(),
        check_dependencies(),
        check_cpp_bindings(),
        check_unity_executable(),
        check_config_files()
    ]
    
    if all(checks):
        print("\n🎉 All checks passed! Your environment should be ready.")
    else:
        print(f"\n⚠️  {sum(not check for check in checks)} issues found.")
        suggest_fixes()

if __name__ == "__main__":
    main()