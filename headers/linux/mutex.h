#ifndef __LINUX_MUTEX_H
#define __LINUX_MUTEX_H

#include <linux/spinlock_types.h>
#include <linux/atomic.h>

struct mutex {	
	// 1: unlocked, 0: locked, negative: locked, possible waiters
	atomic_t		count;
	spinlock_t		wait_lock;
};

#define __MUTEX_INITIALIZER(lockname) \
        { \
					.count = ATOMIC_INIT(1), \
					.wait_lock = __SPIN_LOCK_UNLOCKED(lockname.wait_lock) \
				}

#define DEFINE_MUTEX(mutexname) \
	struct mutex mutexname = __MUTEX_INITIALIZER(mutexname)

void mutex_init(struct mutex *lock) { }
void mutex_lock(struct mutex *lock) { }
void mutex_unlock(struct mutex *lock) { }

#endif
