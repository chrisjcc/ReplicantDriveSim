# Clean build directory
rm -rf build

# Create build directory and navigate to it
mkdir build
cd build

# Configure the project
cmake ..

# Build the project
make

# Navigate back to the root directory
cd ..
