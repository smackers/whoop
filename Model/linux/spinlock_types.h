#ifndef __LINUX_SPINLOCK_TYPES_H
#define __LINUX_SPINLOCK_TYPES_H

typedef struct {
    int init;
    int locked;
} spinlock_t;

typedef struct {
  int something;
}  rwlock_t;

# define __SPIN_LOCK_UNLOCKED \
		{ .init = 1,	\
      .locked = 0 \
		}

#define SPIN_LOCK_UNLOCKED	__SPIN_LOCK_UNLOCKED
#define DEFINE_SPINLOCK(x)	spinlock_t x = __SPIN_LOCK_UNLOCKED

#endif /* __LINUX_SPINLOCK_TYPES_H */
