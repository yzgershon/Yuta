# Contributing

Bug reports, accessibility feedback, and design suggestions are welcome through [GitHub Issues](https://github.com/yzgershon/Yuta/issues). Search existing issues first and describe one concrete problem per report.

The source is public with all rights reserved. Before preparing a code contribution, open an issue to discuss the change and its permission terms with the maintainer. This guide does not grant an open-source license or transfer anyone's rights.

## Useful reports

Include Windows version, processor architecture, display scale, Yuta revision, exact steps, expected behavior, and observed behavior. If relevant, identify the media player or browser. Use sample clipboard text and remove private information from screenshots and logs.

Report security vulnerabilities through the private route in [SECURITY.md](SECURITY.md).

## Development expectations

Keep a change focused and explain its user-visible effect. Preserve keyboard navigation, visible focus, reduced motion, existing profiles, and native/demo boundaries. Avoid blocking Windows integration reads on the UI thread or rebuilding the entire menu for a small update.

Run the relevant checks from [Building and testing](docs/BUILDING.md). For interface changes, include a capture at normal scale and a narrow/short-window check. For integration changes, state which Windows hardware or provider was actually exercised. Do not describe a mocked test as a live integration result.

A pull request should state the problem, final behavior, verification performed, and remaining limitations. Do not include generated build output, local profiles, credentials, or machine-specific development notes.
