#ifndef __LINUX_MUTEX_H
#define __LINUX_MUTEX_H

#include <smack.h>

#ifndef UNLOCKED
#define UNLOCKED 0
#endif

struct mutex {
	int status;
	int holder;
};

struct mutex *mutex_init(struct mutex *lock)
{
	struct mutex *mutex = (struct mutex *) malloc(sizeof(struct mutex *));
	mutex->status = UNLOCKED;
	return mutex;
}

void mutex_lock(struct mutex *lock)
{
	__SMACK_code("call corral_atomic_begin();");
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	// __SMACK_code("assert @ != @;", tid, lock->holder);
	__SMACK_code("assume @ == @;", lock->status, UNLOCKED);
	lock->holder = tid;
	__SMACK_code("call corral_atomic_end();");
}

void mutex_unlock(struct mutex *lock)
{
	__SMACK_code("call corral_atomic_begin();");
	int tid = __SMACK_nondet();
	__SMACK_code("call @ := corral_getThreadID();", tid);
	// __SMACK_code("assert @ == @;", tid, lock->holder);
	lock->status = UNLOCKED;
	__SMACK_code("call corral_atomic_end();");
}

#endif
