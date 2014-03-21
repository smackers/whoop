#ifndef _LINUX_TIMER_H
#define _LINUX_TIMER_H

struct timer_list {
	unsigned long expires;
};

int mod_timer(struct timer_list *timer, unsigned long expires);
int del_timer_sync(struct timer_list *timer);

#endif
