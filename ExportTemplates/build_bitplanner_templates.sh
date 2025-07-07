#!/bin/sh

scons platform=windows lto=full target=template_release arch=x86_64 use_llvm=true
scons platform=linuxbsd lto=full target=template_release arch=x86_64
