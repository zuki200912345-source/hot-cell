import{readFile,stat,mkdir,cp}from'node:fs/promises';
import{execFileSync}from'node:child_process';
for(const file of['index.html','style.css','renderer.js','rules.js','game.js','sw.js']){await stat('public/'+file);if(file.endsWith('.js'))execFileSync(process.execPath,['--check','public/'+file]);}
const html=await readFile('public/index.html','utf8');for(const id of['scene','binder','enter','readout','dose','order'])if(!html.includes('id="'+id+'"'))throw Error('Missing element '+id);
const hosting=JSON.parse(await readFile('.openai/hosting.json','utf8'));if(hosting.static.directory!=='dist'||!hosting.project_id)throw Error('Invalid hosting configuration');
console.log('Static build validated: HTML, JavaScript syntax, required assets and hosting manifest.');

await mkdir('dist',{recursive:true}); await cp('public','dist',{recursive:true});
