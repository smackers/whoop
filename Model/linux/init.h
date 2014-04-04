#ifndef _LINUX_INIT_H
#define _LINUX_INIT_H

#include <linux/types.h>
#include <linux/compiler.h>

#define __init
#define __initdata

#define __exit
#define __exitdata
#define __exit_p(x) x

#define __setup_param(str, unique_id, fn)
#define __setup(str, func)

#define module_init(initfn) static int init_module(void) __attribute__((weakref(#initfn)));
#define module_exit(exitfn) static void cleanup_module(void) __attribute__((weakref(#exitfn)));

#endif /* _LINUX_INIT_H */
