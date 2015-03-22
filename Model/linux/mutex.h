#ifndef __LINUX_MUTEX_H
#define __LINUX_MUTEX_H

#include <linux/types.h>
#include <smack.h>

#ifndef MUTEX_UNINITIALIZED
#define MUTEX_UNINITIALIZED 0
#endif

#ifndef MUTEX_INITIALIZED
#define MUTEX_INITIALIZED 1
#endif

#ifndef MUTEX_UNLOCKED
#define MUTEX_UNLOCKED 0
#endif

struct mutex
{
	int init;
	int locked;
};

#define DEFINE_MUTEX(x) struct mutex x = { MUTEX_INITIALIZED, MUTEX_UNLOCKED }

void mutex_init(struct mutex *lock)
{
	lock->locked = MUTEX_UNLOCKED;
	lock->init = MUTEX_INITIALIZED;
}

void mutex_lock(struct mutex *lock)
{
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	//__SMACK_code("assert @ != @;", tid, lock->locked);
	__SMACK_code("call corral_atomic_begin();");
	__SMACK_code("assume @ == @;", lock->locked, MUTEX_UNLOCKED);
	lock->locked = tid;
	__SMACK_code("call corral_atomic_end();");
}

bool mutex_lock_interruptible(struct mutex *lock)
{
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	//__SMACK_code("assert @ != @;", tid, lock->locked);
	__SMACK_code("call corral_atomic_begin();");
	__SMACK_code("assume @ == @;", lock->locked, MUTEX_UNLOCKED);
	lock->locked = tid;
	__SMACK_code("call corral_atomic_end();");
	return __SMACK_nondet();
}

void mutex_unlock(struct mutex *lock)
{
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	//__SMACK_code("assert @ == @;", tid, lock->locked);
	__SMACK_code("call corral_atomic_begin();");
	lock->locked = MUTEX_UNLOCKED;
	__SMACK_code("call corral_atomic_end();");
}

#endif
