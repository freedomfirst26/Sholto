# Contributing to Sholto

Bug reports, feature ideas, and pull requests are all welcome. Open an issue at
<https://github.com/freedomfirst26/Sholto/issues>, or just send a PR.

For anything about **commercial licensing**, email <freedomfirst26@proton.me>
rather than opening a public issue.

## Licensing of contributions

Sholto is released under the [PolyForm Shield License 1.0.0](LICENSE). That licence
does not let anyone sublicense, so contributions need their own inbound terms.

**Inbound contributions are licensed to the project under the Apache License 2.0**,
and you certify that you have the right to do so with the **Developer Certificate of
Origin** (below). You keep your copyright. Sign off each commit:

```
git commit -s -m "your message"
```

which appends a line like:

```
Signed-off-by: Your Name <your.email@example.com>
```

Signing off certifies the DCO and confirms you contribute your work under Apache-2.0,
so the copyright holder can distribute it as part of Sholto under its own licence and
under commercial licences. Please use your real name and a working email address.

The DCO is a statement that you wrote the contribution or have the right to submit it,
and that you accept it becomes part of a public record. It grants no licence by itself;
the Apache-2.0 grant above does that.

For a substantial contribution, a short Contributor Licence Agreement may be
requested in addition.

## Developer Certificate of Origin 1.1

<https://developercertificate.org/>

```
Developer Certificate of Origin
Version 1.1

Copyright (C) 2004, 2006 The Linux Foundation and its contributors.

Everyone is permitted to copy and distribute verbatim copies of this
license document, but changing it is not allowed.


Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I have the
    right to submit it under the open source license indicated in the file; or

(b) The contribution is based upon previous work that, to the best of my
    knowledge, is covered under an appropriate open source license and I have
    the right under that license to submit that work with modifications,
    whether created in whole or in part by me, under the same open source
    license (unless I am permitted to submit under a different license), as
    indicated in the file; or

(c) The contribution was provided directly to me by some other person who
    certified (a), (b) or (c) and I have not modified it.

(d) I understand and agree that this project and the contribution are public
    and that a record of the contribution (including all personal information
    I submit with it, including my sign-off) is maintained indefinitely and
    may be redistributed consistent with this project or the open source
    license(s) involved.
```

## Third-party code

Sholto depends on external tools and libraries with their own licenses — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). If your contribution adds a
new dependency, add it there in the same PR, and say plainly what license it
carries. Dependencies with non-commercial or copyleft terms need discussion
first — they can block commercial licences.

Maintainer reminder: the first merged external PR must add an Apache-2.0 row to
THIRD-PARTY-NOTICES.md and put the Apache-2.0 text in the release tarball.

## Trademark

"Sholto" and the Sholto logo are trademarks of the copyright holder and are not
licensed by the PolyForm Shield License. A modified version for others should have a
different name.
