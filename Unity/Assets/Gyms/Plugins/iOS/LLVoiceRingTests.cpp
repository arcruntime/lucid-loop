// Standalone host test only. Never linked into the iOS player.
#ifdef LLVOICE_RING_TEST_MAIN
#include "LLVoiceRing.h"
#include <cassert>
#include <thread>
#include <iostream>
int main() {
    llvoice::SpscRing<int,8> ring;
    int first[] = {0,1,2,3,4,5,6,7,8}, out[8];
    assert(ring.write(first,9)==8); assert(ring.write(first,1)==0);
    assert(ring.pop(out,3)==3); for(int i=0;i<3;i++) assert(out[i]==i);
    assert(ring.write(first,3)==3); assert(ring.pop(out,8)==8);
    for(int i=0;i<5;i++) assert(out[i]==i+3);
    for(int i=5;i<8;i++) assert(out[i]==i-5);
    assert(ring.pop(out,8)==0);
    ring.write(first,8); ring.discardFromConsumer(); assert(ring.available()==0);
    llvoice::SpscRing<llvoice::CaptureSample,8> tagged;
    llvoice::CaptureSample old = {.5f,1}, fresh = {.2f,2}, sample;
    tagged.write(&old,1); tagged.discardFromConsumer();
    // An in-flight old-epoch producer may finish after discard; consumer fences it.
    tagged.write(&old,1); tagged.write(&fresh,1);
    assert(tagged.pop(&sample,1)==1 && sample.epoch!=2);
    assert(tagged.pop(&sample,1)==1 && sample.epoch==2);
    llvoice::SpscRing<unsigned,1024> concurrent;
    constexpr unsigned total=1000000;
    std::thread producer([&]{for(unsigned n=0;n<total;) if(concurrent.write(&n,1)) ++n;});
    for(unsigned n=0;n<total;) {unsigned got;if(concurrent.pop(&got,1)){assert(got==n);++n;}}
    producer.join(); assert(concurrent.available()==0);
    std::cout << "PASS ring wrap/full/empty/discard/epoch and 1000000 concurrent samples\n";
}
#endif
