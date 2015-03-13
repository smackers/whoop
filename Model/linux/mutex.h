#ifndef __LINUX_MUTEX_H
#define __LINUX_MUTEX_H

#include <smack.h>

#ifndef MUTEX_UNLOCKED
#define MUTEX_UNLOCKED 0
#endif

struct mutex
{
	int locked;
};

struct mutex *mutex_init(struct mutex *lock)
{
	struct mutex *mutex = (struct mutex *) malloc(sizeof(struct mutex *));
	mutex->locked = MUTEX_UNLOCKED;
	return mutex;
}

void mutex_lock(struct mutex *lock)
{
	__SMACK_code("call corral_atomic_begin();");
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	__SMACK_code("assert @ != @;", tid, lock->locked);
	__SMACK_code("assume @ == @;", lock->locked, MUTEX_UNLOCKED);
	lock->locked = tid;
	__SMACK_code("call corral_atomic_end();");
}

void mutex_unlock(struct mutex *lock)
{
	__SMACK_code("call corral_atomic_begin();");
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	__SMACK_code("assert @ == @;", tid, lock->locked);
	lock->locked = MUTEX_UNLOCKED;
	__SMACK_code("call corral_atomic_end();");
}

#endif
