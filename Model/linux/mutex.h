#ifndef __LINUX_MUTEX_H
#define __LINUX_MUTEX_H

#include <linux/spinlock_types.h>
#include <linux/atomic.h>

struct mutex {	
	atomic_t count;
	spinlock_t wait_lock;
};

struct mutex *mutex_init(struct mutex *lock)
{	
	return (struct mutex *) malloc(sizeof(struct mutex *));
}

void mutex_lock(struct mutex *lock) { }
void mutex_unlock(struct mutex *lock) { }

#endif
