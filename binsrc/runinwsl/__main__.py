#! /usr/bin/env python3

import os
import subprocess
import sys

def print_usage():
    """Print usage"""
    print ("runinwsl: error in usage; should only be called by genie")
    print ("runinwsl <ewd> <command line>")

def entrypoint():
    """Entrypoint"""
    args = sys.argv[1:]

    if len(args) < 2:
        print_usage()
        return 127

    ewd = args[0]
    cmd = args[1:]

    # Change to correct working directory
    os.chdir (ewd)

    try:
        sp = subprocess.run (cmd)
        exit (sp.returncode)
    except Exception as e:
      print (f"runinwsl: error running command '{cmd}': {e.strerror}")
      exit (127)

entrypoint()
