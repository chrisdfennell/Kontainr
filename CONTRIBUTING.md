# Contributing to Kontainr

Thanks for your interest in contributing!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/Kontainr.git`
3. Create a branch: `git checkout -b feature/my-feature`
4. Make your changes
5. Test locally: `cd Kontainr && dotnet run`
6. Commit and push to your fork
7. Open a Pull Request against the `dev` branch

## Development Setup

- .NET 10 SDK
- Docker (for testing the Docker features)
- Any IDE — VS Code, Visual Studio, Rider

## Branch Strategy

- `main` — stable releases
- `dev` — active development, CI/CD pushes to Docker Hub from here

## Code Style

- Follow existing patterns in the codebase
- Use meaningful commit messages
- Keep PRs focused — one feature or fix per PR

## Reporting Issues

Open an issue on GitHub with:
- What you expected to happen
- What actually happened
- Steps to reproduce
- Your environment (OS, Docker version, browser)

## Security

If you discover a security vulnerability, please open a private issue or contact the maintainer directly rather than posting publicly.
