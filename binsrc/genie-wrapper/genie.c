/*
 * A simple wrapper to execute genie as setuid.
 */

#include <stdio.h>
#include <unistd.h>

#include <sys/types.h>
#include <pwd.h>
#include <stdlib.h>

int main(int argc, char ** argv)
{
        /* Set GENIE_LOGNAME environment variable. */
        struct passwd * logname = getpwuid(getuid());
        int result = setenv ("GENIE_LOGNAME", logname->pw_name, 1);

        if (result != 0)
        {
          perror ("genie-wrapper-logname");
          return 1;
        }

        /* Reset uid/gid */
        setregid(getegid(), getegid());
        setreuid(geteuid(), geteuid());

        /* Attempt to execute script */
        execv("/usr/lib/genie/genie", argv);
        // execv("/bin/sh", argv);

        /* Reach here if execv failed */
        perror("genie-wrapper");
        return 1;
}
