# Resource Tuning Notes

## Targets

- keep container memory ceilings below 256 MB where possible
- prefer alpine or otherwise lightweight images
- avoid optional heavyweight platform services unless explicitly required
- keep background worker concurrency at 1 on single-device deployments