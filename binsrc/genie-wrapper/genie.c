/*
 * A simple wrapper to execute genie as setuid.
 */

#include <stdio.h>
#include <unistd.h>

int main(int argc, char ** argv)
{
        /* Reset uid/gid */
        setregid(getegid(), getegid());
        setreuid(geteuid(), geteuid());

        /* Attempt to execute script */
        execv("/usr/lib/genie", argv);

        /* Reach here if execv failed */
        perror("genie-wrapper");
        return 1;
}
