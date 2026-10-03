# Codex Environment Rules

This repository is running inside an isolated disposable
development VM.

## Permitted operations

You may:

- inspect repository files
- modify repository files
- create files within the repository
- delete files within the repository
- install project-local dependencies
- build the project
- run tests
- run development tools
- create Git branches
- create Git commits

## Git restrictions

Never:

- force push
- modify the main branch directly
- delete the main branch
- rewrite remote history
- modify GitHub security settings

Work only on codex/* development branches.

## System restrictions

Do not:

- attempt privilege escalation
- modify /etc
- modify the host operating system
- disable security controls
- access another user's home directory
- modify firewall configuration
- access VMware host resources
- mount additional filesystems

## Infrastructure

Do not access production infrastructure.

Do not access production:

- databases
- Solr
- Redis
- storage accounts
- Key Vaults
- virtual machines

Do not modify Azure IAM/RBAC.

If a task requires production access, stop and request
human approval.

## Workspace

Normal development work must remain within:

/workspace
