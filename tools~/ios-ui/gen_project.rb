#!/usr/bin/env ruby
# Regenerates SpringBoardTap.xcodeproj — a project whose ONLY target is the
# `SpringBoardTapUITests` UI-testing bundle, with no host application: the
# test drives SpringBoard (`com.apple.springboard`) and whatever app the
# harness pre-installed on the simulator, both by bundle id.
#
# The generated project is committed (its .pbxproj is the artefact CI builds),
# so this script only needs to run again when the target's shape changes —
# never on the CI runner. It uses the `xcodeproj` gem (the one CocoaPods is
# built on): `gem install --user-install xcodeproj`, then
# `ruby tools~/ios-ui/gen_project.rb`. Runs on Linux too — nothing here needs
# Xcode.
require "xcodeproj"

root = File.expand_path(__dir__)
project_path = File.join(root, "SpringBoardTap.xcodeproj")
target_name = "SpringBoardTapUITests"

project = Xcodeproj::Project.new(project_path)
# iOS 16 is the floor the test could ever meet: every runner image ships iOS
# 18.x or 26.x runtimes, and XCUIElement.press(forDuration:) predates it by years.
target = project.new_target(:ui_test_bundle, target_name, :ios, "16.0")

group = project.main_group.new_group(target_name, target_name)
source = group.new_file("#{target_name}.swift")
target.add_file_references([source])

target.build_configurations.each do |config|
  s = config.build_settings
  s["PRODUCT_BUNDLE_IDENTIFIER"] = "com.quickactions.springboardtap"
  s["SWIFT_VERSION"] = "5.0"
  s["GENERATE_INFOPLIST_FILE"] = "YES"
  s["CURRENT_PROJECT_VERSION"] = "1"
  s["MARKETING_VERSION"] = "1.0"
  s["TARGETED_DEVICE_FAMILY"] = "1,2"
  s["SDKROOT"] = "iphoneos"
  s["SUPPORTED_PLATFORMS"] = "iphonesimulator iphoneos"
  # Simulator-only in CI, unsigned on purpose: no Apple account is involved.
  s["CODE_SIGNING_ALLOWED"] = "NO"
  s["CODE_SIGNING_REQUIRED"] = "NO"
  s["CODE_SIGN_IDENTITY"] = ""
  # A UI-testing bundle with "Target to be Tested: None" — the test launches
  # apps itself through XCUIApplication(bundleIdentifier:). TEST_HOST must stay
  # unset too (Apple: USES_XCTRUNNER and TEST_HOST are mutually exclusive).
  s.delete("TEST_TARGET_NAME")
  s.delete("TEST_HOST")
  s.delete("BUNDLE_LOADER")
end

project.save

# The shared scheme is what `xcodebuild test -scheme SpringBoardTap` runs.
scheme = Xcodeproj::XCScheme.new
scheme.add_test_target(target)
scheme.test_action.code_coverage_enabled = false
scheme.save_as(project_path, "SpringBoardTap", true)

puts "wrote #{project_path} (target #{target_name}) and the shared scheme SpringBoardTap"
