# Restore points: retention and a restore menu

Found in manual test 1.2 (restore and restore points). Not built yet.

## 1. Retention by age

Restore points are kept by count only (newest 10, never fewer than 2, folder capped at 500 MB; see `docs/design/backup-restore.md`). They do not expire, so old points pile up.

- Delete restore points older than 30 days. Keep the count and size limits as they are.
- Never delete the newest 2, even when they are older than 30 days.
- Run the check at server start and after each new restore point is written.
- Deletion is a plain file delete of a restore point the app wrote itself; it is not a profile or backup delete.

## 2. Restore menu opens a modal

Today File > Restore opens the native Open dialog straight away. Change it to a modal with two choices:

- **Restore from file** (the current Open dialog flow).
- **Restore from a restore point**: a compact list of the automatic restore points, newest first, with the date and time, the reason (`before restore`, `before delete`, `before import`, `update`) and the size. Select one and restore it, using the same ticked-items compare step as a file restore.

Notes:

- The list reads only `restore-points/` in the data folder. Files the app did not write there are not listed.
- All new text goes into the i18n files (`tr` and `en`), never hard-coded in components.
- Keep the restore-points folder path out of the UI; show the date and reason only.

## Test 1.2 result (for reference)

Restore from a restore point worked. The Restore menu has no list of restore points, so test 1.2 step 5 cannot be done as written. Retest after this is built.
