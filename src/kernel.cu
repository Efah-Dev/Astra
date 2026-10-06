// Astra CUDA core. Field elements are little-endian 8 x 32-bit limbs.
typedef unsigned int u32;
typedef unsigned long long u64;
struct F { u32 v[8]; };
struct P { F x,y; int inf; };
__device__ __constant__ P laneTable[128];
__device__ __constant__ P stepTable[65];
struct Rule { int tron,sensitive,plen,slen,repeat,clen; char prefix[41],suffix[41],chars[59]; };
struct Hit { u64 offset; unsigned char pub[64]; char address[43]; int found; };
__device__ __constant__ Rule rule;
__device__ F one(){ F r={{1,0,0,0,0,0,0,0}}; return r; }
__device__ bool zero(F a){u32 r=0; for(int i=0;i<8;i++)r|=a.v[i];return !r;}
__device__ bool eq(F a,F b){u32 r=0;for(int i=0;i<8;i++)r|=a.v[i]^b.v[i];return !r;}
__device__ F norm(F a){
 bool ge=true; for(int i=7;i>=0;i--){u32 p=i==0?0xfffffc2fU:(i==1?0xfffffffeU:0xffffffffU);if(a.v[i]!=p){ge=a.v[i]>p;break;}}
 if(ge){u64 borrow=0;for(int i=0;i<8;i++){u32 p=i==0?0xfffffc2fU:(i==1?0xfffffffeU:0xffffffffU);u64 sub=(u64)p+borrow;u32 x=a.v[i];a.v[i]=x-(u32)sub;borrow=(u64)x<sub;}}return a;
}
__device__ F fold(F a,u64 h){while(h){u64 c=(u64)a.v[0]+977*h;a.v[0]=(u32)c;c=(c>>32)+a.v[1]+h;a.v[1]=(u32)c;for(int i=2;i<8;i++){c=(c>>32)+a.v[i];a.v[i]=(u32)c;}h=c>>32;}return norm(a);}
__device__ F add(F a,F b){u64 c=0;for(int i=0;i<8;i++){c+=(u64)a.v[i]+b.v[i];a.v[i]=(u32)c;c>>=32;}return fold(a,c);}
__device__ F sub(F a,F b){u64 borrow=0;for(int i=0;i<8;i++){u64 s=(u64)b.v[i]+borrow;u32 x=a.v[i];a.v[i]=x-(u32)s;borrow=(u64)x<s;}if(borrow){u64 c=977;for(int i=0;i<8;i++){u64 s=c+(i==1?1:0);u32 x=a.v[i];a.v[i]=x-(u32)s;c=(u64)x<s;}}return a;}
__device__ F mul(F a,F b){
 u32 t[16]={0};
 #pragma unroll
 for(int i=0;i<8;i++){u64 c=0;
 #pragma unroll
 for(int j=0;j<8;j++){c=(u64)a.v[i]*b.v[j]+t[i+j]+c;t[i+j]=(u32)c;c>>=32;}t[i+8]=(u32)c;}
 F r;u64 c=0;
 #pragma unroll
 for(int i=0;i<8;i++){c+=(u64)t[i]+(u64)t[i+8]*977+(i?t[i+7]:0);r.v[i]=(u32)c;c>>=32;}return fold(r,c+t[15]);
}
__device__ F sq(F a){return mul(a,a);}
__device__ F inv(F a){F r=one();for(int i=255;i>=0;i--){r=sq(r);u32 w=i<32?0xfffffc2dU:(i<64?0xfffffffeU:0xffffffffU);if((w>>(i&31))&1)r=mul(r,a);}return r;}
__device__ P plusInv(P a,P b,F di){
 if(a.inf)return b;if(b.inf)return a;
 if(eq(a.x,b.x)){if(!eq(a.y,b.y)||zero(a.y)){P r=a;r.inf=1;return r;}F three=add(sq(a.x),add(sq(a.x),sq(a.x)));di=inv(add(a.y,a.y));F l=mul(three,di);P r;r.x=sub(sq(l),add(a.x,a.x));r.y=sub(mul(l,sub(a.x,r.x)),a.y);r.inf=0;return r;}
 F l=mul(sub(b.y,a.y),di);P r;r.x=sub(sub(sq(l),a.x),b.x);r.y=sub(mul(l,sub(a.x,r.x)),a.y);r.inf=0;return r;
}
__device__ P plus(P a,P b){return plusInv(a,b,inv(sub(b.x,a.x)));}
__device__ u64 rot(u64 x,int n){return n?(x<<n)|(x>>(64-n)):x;}
__device__ __constant__ u64 RC[24]={0x1ULL,0x8082ULL,0x800000000000808aULL,0x8000000080008000ULL,0x808bULL,0x80000001ULL,0x8000000080008081ULL,0x8000000000008009ULL,0x8aULL,0x88ULL,0x80008009ULL,0x8000000aULL,0x8000808bULL,0x800000000000008bULL,0x8000000000008089ULL,0x8000000000008003ULL,0x8000000000008002ULL,0x8000000000000080ULL,0x800aULL,0x800000008000000aULL,0x8000000080008081ULL,0x8000000000008080ULL,0x80000001ULL,0x8000000080008008ULL};
__device__ __constant__ int RHO[25]={0,1,62,28,27,36,44,6,55,20,3,10,43,25,39,41,45,15,21,8,18,2,61,56,14};
__device__ void keccak(const unsigned char* in,int len,unsigned char* out){
 u64 a[25]={0};for(int i=0;i<len;i++)a[i/8]|=(u64)in[i]<<((i&7)*8);a[len/8]^=1ULL<<((len&7)*8);a[16]^=0x8000000000000000ULL;
 for(int round=0;round<24;round++){u64 c[5],d[5],b[25];for(int x=0;x<5;x++)c[x]=a[x]^a[x+5]^a[x+10]^a[x+15]^a[x+20];for(int x=0;x<5;x++)d[x]=c[(x+4)%5]^rot(c[(x+1)%5],1);for(int y=0;y<5;y++)for(int x=0;x<5;x++)b[y+5*((2*x+3*y)%5)]=rot(a[x+5*y]^d[x],RHO[x+5*y]);for(int y=0;y<5;y++)for(int x=0;x<5;x++)a[x+5*y]=b[x+5*y]^((~b[(x+1)%5+5*y])&b[(x+2)%5+5*y]);a[0]^=RC[round];}
 for(int i=0;i<32;i++)out[i]=(unsigned char)(a[i/8]>>((i&7)*8));
}
__device__ __constant__ u32 K[64]={0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2};
__device__ u32 rr(u32 x,int n){return(x>>n)|(x<<(32-n));}
__device__ void sha(const unsigned char* in,int len,unsigned char* out){u32 w[64]={0};for(int i=0;i<len;i++)w[i/4]|=(u32)in[i]<<(24-(i&3)*8);w[len/4]|=0x80U<<(24-(len&3)*8);w[15]=len*8;for(int i=16;i<64;i++)w[i]=w[i-16]+(rr(w[i-15],7)^rr(w[i-15],18)^(w[i-15]>>3))+w[i-7]+(rr(w[i-2],17)^rr(w[i-2],19)^(w[i-2]>>10));u32 h[8]={0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19};u32 a=h[0],b=h[1],c=h[2],d=h[3],e=h[4],f=h[5],g=h[6],v=h[7];for(int i=0;i<64;i++){u32 t=v+(rr(e,6)^rr(e,11)^rr(e,25))+((e&f)^(~e&g))+K[i]+w[i];u32 s=(rr(a,2)^rr(a,13)^rr(a,22))+((a&b)^(a&c)^(b&c));v=g;g=f;f=e;e=d+t;d=c;c=b;b=a;a=t+s;}u32 z[8]={a,b,c,d,e,f,g,v};for(int i=0;i<8;i++){z[i]+=h[i];for(int j=0;j<4;j++)out[4*i+j]=(unsigned char)(z[i]>>(24-8*j));}}
__device__ char lower(char c){return c>='A'&&c<='Z'?c+32:c;}
__device__ bool same(char a,char b,bool foldcase){return foldcase?lower(a)==lower(b):a==b;}
__device__ bool matches(const char* s,int n,bool foldcase){for(int i=0;i<rule.plen;i++)if(!same(s[i],rule.prefix[i],foldcase))return false;for(int i=0;i<rule.slen;i++)if(!same(s[n-rule.slen+i],rule.suffix[i],foldcase))return false;if(rule.repeat){char c=s[n-1];bool accepted=false;for(int i=0;i<rule.clen;i++)if(same(c,rule.chars[i],foldcase))accepted=true;if(!accepted)return false;for(int i=1;i<rule.repeat;i++)if(!same(c,s[n-1-i],foldcase))return false;}return true;}
__device__ void pubbytes(P p,unsigned char* b){for(int i=0;i<32;i++){b[i]=(unsigned char)(p.x.v[7-i/4]>>(24-(i&3)*8));b[32+i]=(unsigned char)(p.y.v[7-i/4]>>(24-(i&3)*8));}}
__device__ bool address(P p,char* out,bool testAll){
 unsigned char pub[64],h[32];pubbytes(p,pub);keccak(pub,64,h);
 if(rule.tron){unsigned char bytes[25],a[32],b[32];bytes[0]=0x41;for(int i=0;i<20;i++)bytes[i+1]=h[i+12];sha(bytes,21,a);sha(a,32,b);for(int i=0;i<4;i++)bytes[21+i]=b[i];const char* alphabet="123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";for(int j=33;j>=0;j--){int rem=0;for(int i=0;i<25;i++){int x=rem*256+bytes[i];bytes[i]=x/58;rem=x%58;}out[j]=alphabet[rem];}out[34]=0;return matches(out+1,33,!rule.sensitive);}
 const char* hex="0123456789abcdef";out[0]='0';out[1]='x';for(int i=0;i<20;i++){out[2+i*2]=hex[h[12+i]>>4];out[3+i*2]=hex[h[12+i]&15];}out[42]=0;
 if(!testAll&&!matches(out+2,40,true))return false;
 unsigned char check[32];keccak((const unsigned char*)(out+2),40,check);for(int i=0;i<40;i++){int v=(i&1)?check[i/2]&15:check[i/2]>>4;if(out[i+2]>='a'&&v>=8)out[i+2]-=32;}return matches(out+2,40,!rule.sensitive);
}
extern "C" __global__ void initialize(P* states,const P* bases,const int* reset){int g=blockIdx.x,l=threadIdx.x;if(reset[g])states[g*128+l]=plus(bases[g],laneTable[l]);}
// Each block owns an independent random group. Only one result per group can leave the device.
template<bool Audit> __device__ void searchImpl(P* states,Hit* hits,u64 offset,int batch,Hit* auditResults){int g=blockIdx.x,l=threadIdx.x,idx=g*128+l;P base=states[idx];F products[64];F product=one();for(int j=1;j<=batch;j++){F d=sub(stepTable[j].x,base.x);if(zero(d))d=one();product=mul(product,d);products[j-1]=product;}F reciprocal=inv(product);P next=base;
 for(int j=batch;j>=1;j--){F d=sub(stepTable[j].x,base.x);if(zero(d))d=one();F di=mul(reciprocal,j==1?one():products[j-2]);reciprocal=mul(reciprocal,d);P p=plusInv(base,stepTable[j],di);if(j==batch)next=p;if(j<batch&&!p.inf){char addr[43];bool match=address(p,addr,false);if(Audit){Hit* h=&auditResults[g*128*batch+j*128+l];h->offset=offset+l+1+(u64)j*128;h->found=match;pubbytes(p,h->pub);address(p,h->address,true);}if(match&&atomicCAS(&hits[g].found,0,1)==0){hits[g].offset=offset+l+1+(u64)j*128;pubbytes(p,hits[g].pub);for(int k=0;k<43;k++)hits[g].address[k]=addr[k];}}}
 if(!base.inf){char addr[43];bool match=address(base,addr,false);if(Audit){Hit* h=&auditResults[g*128*batch+l];h->offset=offset+l+1;h->found=match;pubbytes(base,h->pub);address(base,h->address,true);}if(match&&atomicCAS(&hits[g].found,0,1)==0){hits[g].offset=offset+l+1;pubbytes(base,hits[g].pub);for(int k=0;k<43;k++)hits[g].address[k]=addr[k];}}states[idx]=next;
}
extern "C" __global__ void search(P* states,Hit* hits,u64 offset,int batch){searchImpl<false>(states,hits,offset,batch,(Hit*)0);}
extern "C" __global__ void searchAudit(P* states,Hit* hits,u64 offset,int batch,Hit* auditResults){searchImpl<true>(states,hits,offset,batch,auditResults);}
// Public test vectors only: return every candidate and match bit to detect false negatives.
extern "C" __global__ void audit(const P* points,Hit* output,int count){int i=blockIdx.x*blockDim.x+threadIdx.x;if(i>=count)return;P p=points[i];output[i].offset=i;pubbytes(p,output[i].pub);output[i].found=!p.inf&&address(p,output[i].address,true);}
extern "C" __global__ void walkAudit(P* states,Hit* output,int count){int i=blockIdx.x*blockDim.x+threadIdx.x;if(i>=count)return;P p=plus(states[i],stepTable[1]);states[i]=p;pubbytes(p,output[i].pub);output[i].found=!p.inf&&address(p,output[i].address,true);}
