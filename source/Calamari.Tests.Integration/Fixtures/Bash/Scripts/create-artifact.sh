#!/bin/bash

rm -fr ./subdir
mkdir -p ./subdir/anotherdir
touch ./subdir/anotherdir/myfile

new_octopusartifact "./subdir/anotherdir/myfile"