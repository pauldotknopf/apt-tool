#!/usr/bin/env bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd $DIR

export APT_CONFIG=./work/.apt/tmp-apt.conf

apt-cache $*