#ifndef _BARRIER_H
#define _BARRIER_H

void barrier(void);

void wmb(void);

void smp_mb(void);
void smp_wmb(void);

#endif /* _BARRIER_H */
