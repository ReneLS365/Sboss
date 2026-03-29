# Task 5E – Deployment Pipelines

## Goal
Set up and finalize deployment pipelines for distributing the game to the App Store and Play Store.  This includes building release candidates, signing packages, configuring CI/CD pipelines, and ensuring reproducible releases.

## Phase Check
- **Current task:** 5E — Deployment Pipelines.
- **Next task:** None (release).
- **Why allowed:** With MVP complete and QA finished, the last step is to ensure that deployment is automated and reliable.

## Scope
### In scope
- Define CI/CD pipelines (GitHub Actions or other) for building, packaging, and signing client builds for iOS and Android.
- Configure server deployment pipelines for backend services, including Docker image builds and cloud infrastructure deployments.
- Securely manage signing keys and secrets using appropriate secrets management.
- Document deployment steps and rollback procedures.
### Out of scope
- Client feature work or backend business logic updates.
- Major infrastructure migration (e.g. switching cloud providers), unless already planned.

## Allowed Files
- `.github/workflows/**` or equivalent CI/CD pipeline directories.
- Infrastructure-as-code scripts (e.g. Terraform) if used for deployment.
- Build configuration files for iOS/Android.
- `docs/tasks/5E_Deployment_Pipelines.md`.

## Forbidden Files
- Game logic files unrelated to deployment.

## Acceptance Criteria
1. CI/CD pipelines build, sign, and package client apps for both iOS and Android.
2. Backend services can be deployed via pipeline to production/staging environments reproducibly.
3. Deployment secrets (signing keys, API tokens) are securely stored and accessed.
4. Documentation exists for running the pipeline and performing rollbacks.

## Validation
Perform dry runs of the pipelines in staging and verify that artifacts are produced and deployments succeed. Include manual review of build artifacts and signature validity.

## Notes
- Coordinate with app store policies and submission requirements; final submission may require additional manual steps.