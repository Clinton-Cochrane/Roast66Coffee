# Repository Working Agreements

## Topic Branches Are Required

- Read-only inspection and research may be performed on any branch.
- Before making any repository change, including code, tests, configuration, migrations, documentation, or generated artifacts, check the current branch and working-tree status.
- Every distinct work topic must use a fresh, non-default topic branch. Create and switch to that branch before the first edit.
- Use the `{feature#||bug#}<short-topic-name>` naming convention unless the user explicitly requests another non-default branch name.
- Do not make changes, commits, merges, rebases, or other history-changing operations directly on `main`, `master`, or any other protected default branch.
- Do not reuse a branch whose purpose is unrelated to the current topic.
- If the default branch already contains uncommitted work, preserve it exactly: create the topic branch with the working tree intact, do not discard or rewrite the changes, and tell the user that the existing work moved with the branch.
- Never push directly to a protected default branch. Push only the topic branch and leave integration to a pull request or the user's manual workflow.
- Do not bypass this policy through Git, GitHub CLI, an API, a connector, or another tool.

## Maintainability

- Keep changes simple, clean, readable, and unsurprising.
- Preserve unrelated user changes and keep each branch scoped to its stated topic.
