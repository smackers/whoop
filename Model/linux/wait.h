#ifndef _LINUX_WAIT_H
#define _LINUX_WAIT_H

#include <linux/list.h>
#include <linux/stddef.h>
#include <linux/spinlock.h>

typedef struct __wait_queue wait_queue_t;

struct __wait_queue_head {
	spinlock_t		lock;
};
typedef struct __wait_queue_head wait_queue_head_t;


#endif /* _LINUX_WAIT_H */
