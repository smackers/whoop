#ifndef _LINUX_MM_H
#define _LINUX_MM_H

#include <linux/mm_types.h>

static inline void *page_address(struct page *page)
{
	return page->data;
}

#endif /* _LINUX_MM_H */
