import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const WebSocket = require('C:/Users/crkon/AppData/Local/npm-cache/_npx/67eb4586ca667318/node_modules/ws');
let raw=''; for await (const part of process.stdin) raw+=part;
let input;try{input=JSON.parse(raw);}catch{console.log(JSON.stringify({blocked:'invalid-input'}));process.exit(1);}raw='';
const origin='https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app';
const liveSockets=new Set();
if(input.origin!==origin || !process.env.VERCEL_OIDC_TOKEN) throw new Error('Preview preflight failed');
async function open(path, credential={}) {
  return new Promise((resolve,reject)=>{
    const socket=new WebSocket(origin.replace('https:','wss:')+path,{headers:{
      'x-vercel-trusted-oidc-idp-token':process.env.VERCEL_OIDC_TOKEN,...credential}});
    liveSockets.add(socket);socket.on('close',()=>liveSockets.delete(socket));
    const pending=new Map(); let buffer='',sequence=0,instance=null;
    const deadline=setTimeout(()=>{socket.terminate();reject(new Error('Handshake timeout'));},25000);
    socket.on('upgrade',res=>instance=res.headers['x-viettable-instance']??null);
    socket.on('unexpected-response',(_,res)=>{clearTimeout(deadline);res.resume();socket.terminate();resolve({denied:res.statusCode});});
    socket.on('error',()=>{clearTimeout(deadline);reject(new Error('Transport failure'));});
    socket.on('open',()=>socket.send('{"protocol":"json","version":1}\x1e'));
    socket.on('message',bytes=>{
      buffer+=bytes.toString(); const frames=buffer.split('\x1e');buffer=frames.pop();
      for(const rawFrame of frames){if(!rawFrame)continue;let frame;try{frame=JSON.parse(rawFrame);}catch{continue;}
        if(frame.type===undefined){clearTimeout(deadline);if(frame.error){socket.close();reject(new Error('Handshake denied'));continue;}
          resolve({instance,close:()=>socket.close(),invoke:(target,args)=>new Promise((done,fail)=>{
            const id=String(++sequence),timer=setTimeout(()=>{pending.delete(id);fail(new Error('Invocation timeout'));},12000);
            pending.set(id,{done,timer});socket.send(JSON.stringify({type:1,invocationId:id,target,arguments:args})+'\x1e');
          })});
        } else if(frame.type===3 && pending.has(frame.invocationId)){
          const request=pending.get(frame.invocationId);pending.delete(frame.invocationId);clearTimeout(request.timer);
          request.done({rejected:Boolean(frame.error)});
        }
        // Do not print unsolicited business events or any credentials.
      }
    });
  });
}
let failed=false;
function check(name,pass,extra={}){console.log(JSON.stringify({check:name,pass,...extra}));if(!pass)failed=true;}
try{
  const anonymous=await open('/hubs/mobile-notifications');check('anonymous-mobile-denied',[401,403].includes(anonymous.denied),{http:anonymous.denied});
  const staffDenied=await open('/hubs/customer-notifications',{Authorization:'Bearer '+input.customerToken});
  check('customer-staff-hub-denied',[401,403].includes(staffDenied.denied),{http:staffDenied.denied});
  if(staffDenied.close)staffDenied.close();
  const mobile=await open('/hubs/mobile-notifications',{Authorization:'Bearer '+input.customerToken});
  for(const [method,args] of [['SubscribeBranch',[3]],['SubscribeTable',[37]],['SubscribeCustomer',[999999]]])
    check('customer-cannot-select-group-'+method,(await mobile.invoke(method,args)).rejected);
  mobile.close();
  for(const actor of input.staff){
    for(let round=0;round<2;round++){
      const hub=await open('/hubs/customer-notifications',{Cookie:actor.cookie});
      if(hub.denied){check(actor.role+'-hub-auth',false,{http:hub.denied});continue;}
      check(actor.role+'-own-branch-'+round,!(await hub.invoke('SubscribeBranch',[actor.branchId])).rejected,{instance:hub.instance});
      check(actor.role+'-wrong-branch-'+round,(await hub.invoke('SubscribeBranch',[1])).rejected);
      check(actor.role+'-legacy-table-denied-'+round,(await hub.invoke('SubscribeTable',[1])).rejected);
      hub.close();
    }
  }
}catch{console.log(JSON.stringify({blocked:'transport-or-invocation'}));failed=true;}
finally{for(const socket of liveSockets)socket.terminate();input.customerToken=null;for(const actor of input.staff)actor.cookie=null;}
process.exitCode=failed?1:0;
