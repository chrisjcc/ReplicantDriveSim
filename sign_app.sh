#!/bin/bash

# Script to properly sign the Unity app bundle for macOS

APP_PATH="replicantdrivesim/Builds/StandaloneOSX/libReplicantDriveSim.app"

echo "Signing native libraries..."
codesign --sign - --force "$APP_PATH/Contents/PlugIns/libOpenDrive.dylib" || echo "Failed to sign libOpenDrive.dylib"
codesign --sign - --force "$APP_PATH/Contents/PlugIns/libReplicantDriveSim.dylib" || echo "Failed to sign libReplicantDriveSim.dylib"

echo "Signing app frameworks and binaries..."
find "$APP_PATH" -name "*.dylib" -exec codesign --sign - --force {} \; || echo "Some dylib signing failed"
find "$APP_PATH" -name "*.so" -exec codesign --sign - --force {} \; || echo "Some so signing failed"

echo "Signing main executable..."
codesign --sign - --force "$APP_PATH/Contents/MacOS/UnityReplicantDriveSim" || echo "Failed to sign main executable"

echo "Creating entitlements file for app bundle..."
cat > app_entitlements.plist << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
</dict>
</plist>
EOF

echo "Signing app bundle with entitlements..."
codesign --sign - --force --entitlements app_entitlements.plist "$APP_PATH" || echo "Failed to sign app bundle"

echo "Done! Testing app signature..."
codesign --verify --verbose "$APP_PATH" 2>&1 || echo "App verification failed"