#ifndef _LINUX_TIMER_H
#define _LINUX_TIMER_H

struct timer_list {
    unsigned long expires;
    
		void (*function)(unsigned long);
    unsigned long data;
		
		int slack;
		
#ifdef CONFIG_TIMER_STATS
	int start_pid;
	void *start_site;
	char start_comm[16];
#endif
};

#endif
