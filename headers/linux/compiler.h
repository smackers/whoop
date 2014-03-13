#ifndef __LINUX_COMPILER_H
#define __LINUX_COMPILER_H

# define __user
# define __kernel
# define __safe
# define __force
# define __nocast
# define __iomem

#ifndef __must_check
#define __must_check
#endif

#define likely(x)	x
#define unlikely(x)	x

void barrier(void);

#endif /* __LINUX_COMPILER_H */
