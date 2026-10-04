# KiWeave action-pack repository layout

This folder is a publishable, metadata-only repository root. `index.json` is
the signed catalog consumed by the local repository model. Pack files are
reviewed declarative `.kiweavepack` files; each published pack must have
matching `.sig` and `.pub` detached-RSA sidecars before KiWeave treats it as
verified.

Publishing is intentionally external: copy this folder to the chosen HTTPS
host, sign the exact UTF-8 bytes of `index.json`, and place the detached
signature and public-key sidecars beside it. KiWeave never downloads or runs a
pack from the catalog automatically.
