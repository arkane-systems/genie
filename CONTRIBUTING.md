# CONTRIBUTING

First and foremost, thanks for taking the time to contribute! 👍

## How can I contribute?

The _genie_ project welcomes pull requests, whether to address existing issues or for general improvements.

## Pull requests

Please endeavour to maintain the overall style and layout of the project when making a pull request. Also, pull requests should be made against the current _dev_ branch (there will normally be only one branch whose name begins with _dev-_ at a time; if there are more than one, use the highest-numbered branch) rather than the _master_ branch.

If your update significantly changes the behavior of _genie_ , adds new configuration options, etc., please update the README file and the man page as well.

If you have or would like to create the pull request (for comment, for example) but it still requires more work before merging, please tag it _work-in-progess_ .

Thank you!


## Building

Builds are carried out automatically by GitHub Actions on pull-request or push to master. The only build I use
locally is the Debian one, as that's my build platform. If you change any of the others and they stop working on
GitHub Actions, the pull request will not be accepted even if they do work for you locally.

The Arch build makes use of the special container image `cerebrate/fuckarch:right-in-the-ear`, which exists to deal
with the pain-in-the-ass that is having to compile a second package manager to deal with getting packages from a
different repository, all for the sake of one lousy package.

If you need to know the gory details, the Dockerfile for the image is here:

https://gist.github.com/cerebrate/45daae1bf6ad82ecd041d347bd2b1173
