# Handling a new issue

For the maintainer, in five steps. The same steps apply to the server, phone app and plugin repositories. The labels are described in each
repository's `CONTRIBUTING.md`; the design is in [../plans/issues-and-discussions-plan.md](../plans/issues-and-discussions-plan.md).

1. **Read it.** A new issue already carries `needs-triage` and, when the form or the text answer named one, the area and `regression`.
   Is it a question? Move it to Discussions (the button "Convert to discussion") and close the issue.
2. **Is it complete?** Version, steps and what happened are there? If not, ask one specific question, set `needs-info` and remove `needs-triage`.
3. **Reproduce it.** If it happens for you, set `confirmed` and remove the other status label. If it does not, say what you tried and set `needs-info`.
4. **Priority.** Set `priority: high` only when it blocks people (a crash, lost data, a broken install or update). Nothing else gets a priority label.
5. **Work and close.** `in progress` while someone works on it. Close with a one-line reason: fixed in a version, `duplicate` (link the other issue),
   `invalid` or `wontfix` (say why).

## Answers to copy

**Needs more information:** "Thanks for the report. To look into it I need [the Macro Grid version / the steps / the log lines]. The Help menu shows the version and the log folder is in the tray menu. I have set `needs-info`; I will look again when you reply."

**Duplicate:** "This is the same as #NNN, so I am closing this one to keep the discussion in one place. Please add anything new there."

**Works as designed:** "This is how it is meant to work at the moment ([link to the doc]). If you would like it to change, please open an idea in Discussions and describe what you were trying to do."

**Question, not a bug:** "This looks like a question rather than a bug, so I moved it to Discussions where others can find the answer too."

**Alpha limit:** "This is a known limit of the alpha ([roadmap or known gaps]). Thank you for reporting it; I will link this issue when it is picked up."

## Rules of thumb

- One status label at a time. Do not close reports on a timer: an alpha with one maintainer should not lose people's reports to a bot.
- Never paste someone's PIN, token or address back into a reply; ask them to edit it out of their comment.
- Reports of a security problem in a public place: hide the comment, ask the person to use the private form, and continue there.
