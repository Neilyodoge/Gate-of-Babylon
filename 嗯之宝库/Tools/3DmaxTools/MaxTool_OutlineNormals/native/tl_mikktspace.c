/* TL array adapter. Upstream mikktspace.c/h are unmodified. */
#include "mikktspace.h"
#include <stddef.h>

typedef struct { int faces; const float *input; float *output; } TLMesh;
static int face_count(const SMikkTSpaceContext *ctx) { return ((TLMesh *)ctx->m_pUserData)->faces; }
static int corner_count(const SMikkTSpaceContext *ctx, int face) { (void)ctx; (void)face; return 3; }
static void position(const SMikkTSpaceContext *ctx, float out[], int face, int corner) {
    const float *p = ((TLMesh *)ctx->m_pUserData)->input + (face * 3 + corner) * 8;
    out[0]=p[0];out[1]=p[1];out[2]=p[2];
}
static void normal(const SMikkTSpaceContext *ctx, float out[], int face, int corner) {
    const float *p = ((TLMesh *)ctx->m_pUserData)->input + (face * 3 + corner) * 8;
    out[0]=p[3];out[1]=p[4];out[2]=p[5];
}
static void texcoord(const SMikkTSpaceContext *ctx, float out[], int face, int corner) {
    const float *p = ((TLMesh *)ctx->m_pUserData)->input + (face * 3 + corner) * 8;
    out[0]=p[6];out[1]=p[7];
}
static void store_tangent(const SMikkTSpaceContext *ctx, const float tangent[], float sign, int face, int corner) {
    float *p = ((TLMesh *)ctx->m_pUserData)->output + (face * 3 + corner) * 4;
    p[0]=tangent[0];p[1]=tangent[1];p[2]=tangent[2];p[3]=sign;
}
__declspec(dllexport) int tl_mikk_version(void) { return 1; }
__declspec(dllexport) int tl_mikk_generate(int faces, const float *input, float *output) {
    if (faces<=0 || input==NULL || output==NULL) return 0;
    TLMesh mesh = {faces,input,output};
    SMikkTSpaceInterface api = {0};
    api.m_getNumFaces=face_count;api.m_getNumVerticesOfFace=corner_count;
    api.m_getPosition=position;api.m_getNormal=normal;api.m_getTexCoord=texcoord;
    api.m_setTSpaceBasic=store_tangent;
    SMikkTSpaceContext ctx = {&api,&mesh};
    return genTangSpaceDefault(&ctx);
}
