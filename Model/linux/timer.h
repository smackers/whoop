#ifndef _LINUX_TIMER_H
#define _LINUX_TIMER_H

struct timer_list {
	unsigned long expires;
	
	void (*function)(unsigned long);
	unsigned long data;
};

void init_timer(struct timer_list * timer);
void add_timer_on(struct timer_list *timer, int cpu);
void add_timer(struct timer_list *timer);
int del_timer(struct timer_list * timer);
int mod_timer(struct timer_list *timer, unsigned long expires);

#endif
