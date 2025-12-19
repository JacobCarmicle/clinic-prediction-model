#pragma once
#include <stdint.h>

typedef struct {
    uint32_t state;

}XorShift32;

static inline void prng_seed(XorShift32* rng, uint32_t seed){
    //xorshift32 cannot be seeded with 0 safely; tweak if needed
    rng->state = (seed == 0u) ? 0xA341316Cu : seed;
}

static inline uint32_t prng_next_u32(XorShift32* rng){
    uint32_t x = rng->state;
    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;
    rng->state = x;
    return x;
}

//uniform float in [0,1]
static inline float prng_next_f01(XorShift32* rng){
    //take top 24 bits -> 0..(2^24-1) then divide
    uint32_t v = prng_next_u32(rng) >> 8;
    return (float)v * (1.0f / 16777216.0f);
}
