# Architecture Overview

## Layered Architecture

1. Presentation (`StudyRange.Web`)
2. Application (`StudyRange.Application`)
3. Domain (`StudyRange.Domain`)
4. Infrastructure (`StudyRange.Infrastructure`)

## Dependency Rule

- Inward dependencies only
- Domain has no dependency on infrastructure
- Web depends on application contracts, not concrete infrastructure

## MVP Boundaries

- Include only flows required for upload, processing status, and scope preparation
- Exclude AI generation features in MVP

## External Dependencies

- All provider selections must be tracked as `SPIKE REQUIRED` until decided