#ifndef _LINUX_INTERRUPT_H
#define _LINUX_INTERRUPT_H

#include <linux/workqueue.h>
#include <linux/atomic.h>

typedef int irqreturn_t;

#define IRQ_NONE (0 << 0)
#define IRQ_HANDLED (1 << 0)
#define IRQ_WAKE_THREAD (1 << 1)

#define IRQ_RETVAL(x) ((x) != 0)

#endif /* _LINUX_INTERRUPT_H */
