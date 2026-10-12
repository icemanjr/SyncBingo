# ADR 0007 — ECS Fargate hosting and AWS CDK in C#

**Status:** Accepted

## Context
The project should demonstrate real AWS experience and stay within the C#/.NET stack, with a cost-conscious demo option.

## Decision
Run the API on ECS Fargate behind an ALB, with ElastiCache Redis, an S3 + CloudFront SPA, Route 53 and ACM. Provision everything with AWS CDK written in C#. Deploy from GitHub Actions using OIDC.

## Consequences
- One language across API, tests and infrastructure.
- Long-lived WebSocket connections fit containers better than Lambda.
- Fargate and ElastiCache have a baseline running cost; a teardown path and a minimal "demo mode" are documented.
