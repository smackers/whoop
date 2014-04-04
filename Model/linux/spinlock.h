#ifndef __LINUX_SPINLOCK_H
#define __LINUX_SPINLOCK_H

#include <linux/spinlock_types.h>

void spin_lock_init(spinlock_t *);

void spin_lock(spinlock_t *);
void spin_lock_irqsave(spinlock_t *, unsigned long);
void spin_lock_irq(spinlock_t *);
void spin_lock_bh(spinlock_t *);

void spin_unlock(spinlock_t *);
void spin_unlock_irqrestore(spinlock_t *, unsigned long);
void spin_unlock_irq(spinlock_t *);
void spin_unlock_bh(spinlock_t *);

#endif /* __LINUX_SPINLOCK_H */
