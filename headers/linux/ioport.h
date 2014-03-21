/*
 * ioport.h	Definitions of routines for detecting, reserving and
 *		allocating system resources.
 *
 * Authors:	Linus Torvalds
 */

#ifndef _LINUX_IOPORT_H
#define _LINUX_IOPORT_H

struct resource {
	unsigned long start, end;
	const char *name;
	unsigned long flags;
};

#endif	/* _LINUX_IOPORT_H */
