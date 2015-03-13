#ifndef __LINUX_SPINLOCK_H
#define __LINUX_SPINLOCK_H

#include <smack.h>

#ifndef SPIN_LOCK_UNLOCKED
#define SPIN_LOCK_UNLOCKED 0
#endif

typedef struct
{
  int locked;
} spinlock_t;

#define DEFINE_SPINLOCK(x) spinlock_t x = { SPIN_LOCK_UNLOCKED }

void spin_lock_init(spinlock_t *lock)
{
  lock = (struct spinlock_t *) malloc(sizeof(struct spinlock_t *));
  lock->locked = SPIN_LOCK_UNLOCKED;
}

void spin_lock(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ != @;", tid, lock->locked);
  __SMACK_code("assume @ == @;", lock->locked, SPIN_LOCK_UNLOCKED);
  lock->locked = tid;
  __SMACK_code("call corral_atomic_end();");
}

void spin_lock_irqsave(spinlock_t *lock, unsigned long value)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ != @;", tid, lock->locked);
  __SMACK_code("assume @ == @;", lock->locked, SPIN_LOCK_UNLOCKED);
  lock->locked = tid;
  __SMACK_code("call corral_atomic_end();");
}

void spin_lock_irq(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ != @;", tid, lock->locked);
  __SMACK_code("assume @ == @;", lock->locked, SPIN_LOCK_UNLOCKED);
  lock->locked = tid;
  __SMACK_code("call corral_atomic_end();");
}

void spin_lock_bh(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ != @;", tid, lock->locked);
  __SMACK_code("assume @ == @;", lock->locked, SPIN_LOCK_UNLOCKED);
  lock->locked = tid;
  __SMACK_code("call corral_atomic_end();");
}

void spin_unlock(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ == @;", tid, lock->locked);
  lock->locked = SPIN_LOCK_UNLOCKED;
  __SMACK_code("call corral_atomic_end();");
}

void spin_unlock_irqrestore(spinlock_t *lock, unsigned long value)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ == @;", tid, lock->locked);
  lock->locked = SPIN_LOCK_UNLOCKED;
  __SMACK_code("call corral_atomic_end();");
}

void spin_unlock_irq(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ == @;", tid, lock->locked);
  lock->locked = SPIN_LOCK_UNLOCKED;
  __SMACK_code("call corral_atomic_end();");
}

void spin_unlock_bh(spinlock_t *lock)
{
  __SMACK_code("call corral_atomic_begin();");
  int tid = __SMACK_nondet();
  __SMACK_code("call @ := corral_getThreadID();", tid);
  __SMACK_code("assert @ == @;", tid, lock->locked);
  lock->locked = SPIN_LOCK_UNLOCKED;
  __SMACK_code("call corral_atomic_end();");
}

#endif /* __LINUX_SPINLOCK_H */
