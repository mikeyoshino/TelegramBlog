#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
project=src/TeleGramBlog.iOS/TeleGramBlog.iOS.csproj
app=src/TeleGramBlog.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/TeleGramBlog.iOS.app
# Boot an iPhone Simulator in Xcode first, or set LUMA_SIMULATOR to its UUID.
device=${LUMA_SIMULATOR:-booted}
dotnet build "$project" -p:RuntimeIdentifier=iossimulator-arm64 -p:CodesignKey=-
xcrun simctl install "$device" "$app"
xcrun simctl launch "$device" app.telegramblog.luma "$@"
