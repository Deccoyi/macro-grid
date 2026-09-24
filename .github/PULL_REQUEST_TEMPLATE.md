## What and why

<!-- One topic per pull request. Link the issue if there is one: Closes #123 -->

## Checklist

- [ ] The pull request targets the `dev` branch
- [ ] Commits follow Conventional Commits (`type(scope): description`) and everything is in English
- [ ] `dotnet test` passes, and `npm run typecheck` passes in the parts I touched (`editor`, `webclient`, `packages/renderer`)
- [ ] I added or updated tests
- [ ] I updated both changelogs under `[Unreleased]` (`docs/CHANGELOG-developer.md` and `docs/CHANGELOG.md`)
- [ ] New UI text is in both `tr.ts` and `en.ts`
- [ ] No version numbers were bumped, and protocol changes stay compatible with older clients
- [ ] New dependencies are license-checked and added to `THIRD_PARTY_NOTICES.md`

## Notes for the reviewer

<!-- Screenshots for UI changes, plugin SDK or protocol impact, anything risky. -->
