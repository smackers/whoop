#ifndef _LINUX_WORKQUEUE_H
#define _LINUX_WORKQUEUE_H

#include <linux/timer.h>
#include <linux/atomic.h>

struct work_struct {
    void (*func)(void *);
    void *data;

    int init;
};

#define DECLARE_WORK(n, f, d) \
	struct work_struct n = { \
           .func = (f), \
           .data = (d), \
           .init = 1, \
        }

#define PREPARE_WORK(_work, _func, _data) \
	do { \
		(_work)->func = _func; \
		(_work)->data = _data; \
                (_work)->init = 1; \
	} while (0)

#define INIT_WORK(_work, _func, _data) \
	do { \
		PREPARE_WORK((_work), (_func), (_data)); \
	} while (0)

static bool schedule_work(struct work_struct *work);
void flush_scheduled_work(void);

#endif
