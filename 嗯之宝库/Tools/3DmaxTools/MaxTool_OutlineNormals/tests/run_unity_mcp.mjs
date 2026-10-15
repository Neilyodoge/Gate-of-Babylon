// Use the official Unity MCP server over stdio, including its queue and agent tracking.
// No direct calls to the internal Unity HTTP bridge.
import {Client} from 'file:///C:/Users/admin/AppData/Local/npm-cache/_npx/f0cc91e6da0fedf3/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StdioClientTransport} from 'file:///C:/Users/admin/AppData/Local/npm-cache/_npx/f0cc91e6da0fedf3/node_modules/@modelcontextprotocol/sdk/dist/esm/client/stdio.js';
import {readFileSync} from 'node:fs';
const transport=new StdioClientTransport({command:process.execPath,args:['C:/Users/admin/AppData/Local/npm-cache/_npx/f0cc91e6da0fedf3/node_modules/anklebreaker-unity-mcp/src/index.js'],env:{...process.env,UNITY_BRIDGE_PORT:'7891'},stderr:'pipe'});
const client=new Client({name:'TL Outline validation',version:'1.0'});
try{
 await client.connect(transport);
 const args=process.argv[2]?{port:7891,code:readFileSync(process.argv[2],'utf8')}:{port:7891,code:'return "MCP connected";'};
 const result=await client.callTool({name:'unity_execute_code',arguments:args},undefined,{timeout:180000});
 console.log(JSON.stringify(result));
}finally{await client.close();}
