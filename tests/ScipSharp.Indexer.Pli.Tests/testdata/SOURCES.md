# Test data sources

## Written for this project

- `snapshots/input/basics/*`: fixed-format (`MARGINS(2,72)`) programs covering structures, factored declarations,
  `LIKE`, `BUILTIN`, `%INCLUDE`, nested procedures, BEGIN blocks, labels, ENTRY statements and cross-file external
  procedures.
- `snapshots/input/packages/*`: Enterprise PL/I `PACKAGE`/`EXPORTS`, `DEFINE ORDINAL`/`DEFINE STRUCTURE`, `TYPE`,
  `GENERIC`, `BASED`/`DEFINED`, the macro preprocessor (`%DCL`, `%PROC`, `%IF`), and keywords used as identifiers.

Both are original work, released under the repository licence (Apache-2.0).

## Multics (`corpus/multics/`, `snapshots/input/multics/`)

- Origin: <https://github.com/dancrossnyc/multics>, commit `dc291689edf955c660e57236da694630e2217151`
  (an import of the Multics sources released by Bull HN to MIT, <https://web.mit.edu/multics-history/>).
- Licence: the Multics licence (SPDX `Multics`, a permissive, MIT-style licence): use, copy, modification and
  distribution for any purpose without fee, provided the copyright notice and the historical background appear in all
  copies. Both are reproduced in `Multics_sources.txt` next to the files, and every file keeps its original copyright
  header. Only files without post-MR12.5 changes (no `MR12.6`/`MR12.7` history entries) were taken, so all of them
  are covered by the Bull HN release.
- Files (copied unchanged, flattened into `src/` and `include/`):
  - `library_dir_dir/system_library_1/source/bound_date_time_.s.archive/date_time_.pl1`
  - `library_dir_dir/system_library_1/source/bound_library_1_.s.archive/{com_err_,cv_dec_,requote_string_,sub_err_}.pl1`
  - `library_dir_dir/system_library_1/source/bound_library_wired_.s.archive/ioa_.pl1`
  - `library_dir_dir/system_library_standard/source/bound_fscom2_.s.archive/{copy_dir,copy_dir_}.pl1`
  - `library_dir_dir/system_library_standard/source/bound_info_rtns_.s.archive/list_ref_names.pl1`
  - `library_dir_dir/system_library_standard/source/bound_io_commands_.s.archive/get_line_length_.pl1`
  - `library_dir_dir/system_library_tools/source/ask_.pl1`
  - `library_dir_dir/include/*.incl.pl1`: the include files the above reference (see `corpus/multics/include/`).
- `snapshots/input/multics/` holds copies of `requote_string_.pl1`, `get_line_length_.pl1`, `iox_dcls.incl.pl1` and
  `mode_string_info.incl.pl1` from the same commit.

## IBM Z Open Editor sample (`snapshots/input/zopeneditor/`)

- Origin: <https://github.com/IBM/zopeneditor-sample>, commit `8f9835308de6159eb54f226041d5f81a780cb8eb`.
- Licence: Apache License 2.0; the repository's `LICENSE` is copied next to the file.
- Files: `PLI/MACSAMP.pli` only (copied unchanged; the macro-preprocessor showcase, self-contained).
- Not copied: every other PL/I and COBOL file in that repository (`PSAM1`, `PSAM1LIB`, `PSAM2`, `CUSTPLI.inc`,
  `BALSTATS.inc`, the COBOL samples and copybooks). Although the repository is Apache-2.0, those files carry
  "Licensed Materials - Property of IBM / Restricted Materials of IBM ... All rights reserved" banners.
