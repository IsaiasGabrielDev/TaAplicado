// Synthetic data so the window can be designed in a plain browser (no Photino host).
// Nothing here is real: jobs, companies and answers are invented.
'use strict';
(() => {
  const now = Date.now();
  const t = min => new Date(now - min * 60000).toISOString();
  const state = {
    type: 'state', running: true, stopping: false, attention: null, lang: new URLSearchParams(location.search).get('lang') === 'en' ? 'en' : 'pt', currentJob: '4031772210',
    claudeOk: true, chromeOk: true, riskAccepted: true, dataNoticeAccepted: true, noAi: false, runDryRun: false, resumes: { pt: 'Curriculo_Marina_Alves.pdf', en: null },
    lastRun: { at: t(140), dryRun: false, blocked: { PHP: 1 }, ultra: null, max: 25, counts: { applied: 5, skip: 2, failed: 1, waiting: 3 } },
    filters: { companies: ['Consultoria Exemplo'], words: ['estágio', 'PHP'], useClaude: true, country: 'Brasil', countryMode: 'prefer', language: 'Português', languageMode: 'off' },
    currentUser: 'marina',
    users: [{ id: 'marina', name: 'Marina Alves', avisos: 4 }, { id: 'ana', name: 'Ana Ribeiro', avisos: 0 }, { id: 'bruno', name: 'Bruno', avisos: 2 }],
    // 4031772210 is mid-application; it lands in the ledger when the timeout below delivers it
    avisos: [
      { id: 'a1', jobId: '4029915530', job: 'Desenvolvedor .NET Pleno | Vértice Sistemas', label: 'Quantos anos de experiência você tem com Azure Service Bus?', type: 'number', options: [], suggestion: '1', error: null, at: t(94) },
      { id: 'a2', jobId: '4030118842', job: 'Backend Engineer (C#) | Nortia Pagamentos', label: 'Qual é o seu nível de inglês?', type: 'select', options: ['Básico', 'Intermediário', 'Avançado', 'Fluente'], suggestion: 'Intermediário', error: null, at: t(71) },
      { id: 'a3', jobId: '4030877105', job: 'Software Engineer | Lumen Health', label: 'Are you willing to work on-site in São Paulo three days a week?', type: 'radio', options: ['Yes', 'No'], suggestion: '', error: null, at: t(40) },
      { id: 'a4', jobId: '4031009921', job: 'Analista Desenvolvedor C# | Cooperativa Sul', label: 'Pretensão salarial mensal (CLT)', type: 'text', options: [], suggestion: '9000', error: 'Insira um número inteiro', at: t(12) },
    ],
    ledger: [
      { id: '4031009921', status: 'waiting', job: 'Analista Desenvolvedor C# | Cooperativa Sul', at: t(12) },
      { id: '4030990187', status: 'applied', job: 'Desenvolvedor .NET Sênior | Maré Logística', at: t(18),
        trail: [
          { jobId: '4030990187', text: 'Vaga aberta: Desenvolvedor .NET Sênior | Maré Logística', at: t(21) },
          { jobId: '4030990187', text: 'Anos de experiência com C# = 4', at: t(20.6) },
          { jobId: '4030990187', text: 'Etapa 1 concluída', at: t(20.4) },
          { jobId: '4030990187', text: 'Are you legally authorized to work in Brazil? = Yes', at: t(19.5) },
          { jobId: '4030990187', text: 'Etapa 2 concluída', at: t(19.2) },
          { jobId: '4030990187', text: 'Candidatura enviada', at: t(18.1) },
          { jobId: '4030990187', text: 'Enviada', at: t(18) }],
        sent: [['Anos de experiência com C#', '4'], ['Are you legally authorized to work in Brazil?', 'Yes'], ['Disponibilidade para início', 'Imediata']] },
      { id: '4030877105', status: 'waiting', job: 'Software Engineer | Lumen Health', at: t(40) },
      { id: '4030612335', status: 'skip: blocked: palavra ignorada (PHP)', job: 'Dev Full Stack PHP/.NET | Loja Fictícia', at: t(49) },
      { id: '4030612334', status: 'skip: no easy apply', job: 'Tech Lead .NET | Grupo Ferraz', at: t(47) },
      { id: 'indeed-d5d5601368e67f2c', status: 'applied', job: 'Desenvolvedor Full-stack (.NET C# e Blazor) | Empresa Fictícia', at: t(52) },
      { id: '4030500019', status: 'applied', job: 'Engenheiro de Software C# | Orbe Seguros', at: t(55) },
      { id: '4030118842', status: 'waiting', job: 'Backend Engineer (C#) | Nortia Pagamentos', at: t(71) },
      { id: '4030050771', status: 'failed: stuck on a step', job: 'Desenvolvedor Unity | Estúdio Carambola', at: t(80) },
      { id: '4029915530', status: 'waiting', job: 'Desenvolvedor .NET Pleno | Vértice Sistemas', at: t(94) },
      { id: '4029800012', status: 'applied', job: 'Desenvolvedor Back-end .NET | Paralelo Educação', at: t(120) },
      { id: '4029712004', status: 'applied', job: 'Programador C# (WinForms) | Tecnoagro', at: t(260) },
      { id: '4029600451', status: 'applied', job: 'Desenvolvedor .NET Júnior | Ponte Digital', at: t(1500) },
    ],
    answers: [
      { key: 'anos de experiência com c', label: 'Anos de experiência com C#', answer: '4', by: 'Claude', job: 'Desenvolvedor .NET Sênior | Maré Logística' },
      { key: 'você possui certificação pmp', label: 'Você possui certificação PMP?', answer: 'No', by: 'Claude' },
      { key: 'disponibilidade para início', label: 'Disponibilidade para início', answer: 'Imediata', by: 'você' },
      { key: 'how many years of work experience do you have with sql server', label: 'How many years of work experience do you have with SQL Server?', answer: '3', by: 'Claude' },
      { key: 'are you legally authorized to work in brazil', label: 'Are you legally authorized to work in Brazil?', answer: 'Yes', by: 'Claude' },
    ],
    log: [
      { jobId: null, text: 'Execução iniciada (envio de verdade)', at: t(130) },
      { jobId: '4030612334', text: 'Vaga aberta: Tech Lead .NET | Grupo Ferraz', at: t(47.2) },
      { jobId: '4030612334', text: 'Pulada: sem Easy Apply', at: t(47) },
      { jobId: '4030990187', text: 'Vaga aberta: Desenvolvedor .NET Sênior | Maré Logística', at: t(21) },
      { jobId: '4030990187', text: 'Anos de experiência com C# = 4', at: t(20.6) },
      { jobId: '4030990187', text: 'Candidatura enviada', at: t(18.1) },
      { jobId: '4030990187', text: 'Enviada', at: t(18) },
      { jobId: null, text: 'Próxima vaga em 41 s', at: t(18) },
      { jobId: '4031009921', text: 'Vaga aberta: Analista Desenvolvedor C# | Cooperativa Sul', at: t(13) },
      { jobId: '4031009921', text: 'Claude: 2 pergunta(s) nova(s)', at: t(13) },
      { jobId: '4031009921', text: 'Aguardando você: há avisos para responder', at: t(12) },
      { jobId: '4031772210', text: 'Vaga aberta: Desenvolvedor Full Stack .NET | Arquipélago Tech', at: t(3) },
      { jobId: '4031772210', text: 'Anos de experiência com C# = 4', at: t(2.8) },
      { jobId: '4031772210', text: 'Etapa 1 concluída', at: t(2.6) },
      { jobId: '4031772210', text: 'Disponibilidade para início = Imediata', at: t(2.2) },
      { jobId: '4031772210', text: 'Etapa 2 concluída', at: t(2) },
    ],
    profile: {
      nome: 'Marina Alves', localizacao: 'São Paulo, SP, Brasil', cargoAtual: 'Supervisor de TI',
      cargoDesejado: 'Desenvolvedor .NET sênior', anosDev: '5', anosTi: '7',
      anosTech: 'C# / .NET = 5\nSQL Server = 5\nEntity Framework Core = 4', techNao: 'Docker, Kubernetes, AWS',
      ingles: 'Intermediário (B1)', contrato: 'CLT ou PJ', pretensaoClt: '13500', modelo: 'Remoto',
      resumo: 'Desenvolvedor C#/.NET com foco em APIs, integrações e automação de processos.',
    },
  };

  const push = () => window.onState(structuredClone(state));
  window.demo = {
    handle(m) {
      if (m.type === 'answer') {
        const a = state.avisos.find(v => v.id === m.id);
        state.avisos = state.avisos.filter(v => v.id !== m.id);
        state.answers.push({ key: a.label.toLowerCase(), label: a.label, answer: m.answer, by: 'você' });
        const e = state.ledger.find(x => x.id === a.jobId);
        if (e && !state.avisos.some(v => v.jobId === a.jobId)) e.status = 'ready';
      }
      if (m.type === 'stop') { state.running = false; state.nextAt = null; state.lastRun = { ...state.lastRun, end: 'você parou', endedAt: new Date().toISOString() }; }
      if (m.type === 'start') { state.running = true; state.log.push({ jobId: null, text: m.dryRun ? 'Execução em teste iniciada (nada é enviado)' : 'Execução iniciada (envio de verdade)', at: new Date().toISOString() }); }
      if (m.type === 'saveFilters') state.filters = { companies: m.companies, words: m.words, useClaude: m.useClaude, country: m.country, countryMode: m.countryMode, language: m.language, languageMode: m.languageMode };
      if (m.type === 'start') { state.runUltra = m.ultra; state.runDryRun = m.dryRun; state.runPhase = m.ultra ? `analisando 1 de ${Math.min(m.max * 5, 150)} · 0 com nota ≥ ${m.minScore}` : null; }
      if (m.type === 'saveProfile') state.profile = m.values;
      if (m.type === 'saveAnswer') { const key = m.label.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim(); state.answers = state.answers.filter(a => a.key !== key).concat({ key, label: m.label, answer: m.answer, by: 'você', reviewed: true }); }
      if (m.type === 'ask') {
        state.asking = true;
        setTimeout(() => { state.asking = false; push(); window.onAsk({ message: `Olá! Tudo bem?

1) Tenho 5 anos de experiência com C#.
2) Disponibilidade imediata.
3) [por que a Lumen Health]
4) Não tenho experiência com Kafka.

Obrigada,
Marina`, items: [
          { question: 'Quantos anos de experiência você tem com C#?', answer: '5', confident: true },
          { question: 'Disponibilidade para início', answer: 'Imediata', confident: true },
          { question: 'Por que você quer trabalhar na Lumen Health?', answer: 'Quero aplicar minha experiência com APIs e integrações em .NET num produto de saúde, onde automação de processos tem impacto direto.', confident: false },
          { question: 'Você tem experiência com Kafka?', answer: '', confident: false },
        ] }); }, 1200);
      }
      if (m.type === 'setNoAi') state.noAi = m.on;
      if (m.type === 'setLang') { location.search = m.en ? '?lang=en' : ''; return; } // the real app saves it and reloads
      if (m.type === 'pickResume') state.resumes[m.lang] = m.lang === 'en' ? 'Resume_Marina_Alves.pdf' : 'Curriculo_Marina_Alves.pdf';
      if (m.type === 'removeResume') state.resumes[m.lang] = null;
      if (m.type === 'acceptRisk') state.riskAccepted = true;
      if (m.type === 'acceptDataNotice') state.dataNoticeAccepted = true;
      if (m.type === 'switchUser') state.currentUser = m.id;
      if (m.type === 'createUser') { const id = m.name.toLowerCase(); state.users.push({ id, name: m.name, avisos: 0 }); state.currentUser = id; }
      if (m.type === 'reviewAll') state.answers.forEach(a => { if (a.by !== 'você') a.reviewed = true; });
      if (m.type === 'reviewAnswer') state.answers.find(x => x.key === m.key).reviewed = true;
      if (m.type === 'deleteAnswer') state.answers = state.answers.filter(a => a.key !== m.key);
      if (m.type === 'editAnswer') { const a = state.answers.find(x => x.key === m.key); a.answer = m.answer; a.by = 'você'; }
      push();
    },
  };
  push();
  // one more delivery a few seconds in, so the franking reels visibly roll
  setTimeout(() => {
    const at = new Date().toISOString();
    state.log.push({ jobId: '4031772210', text: 'Candidatura enviada', at }, { jobId: '4031772210', text: 'Enviada', at },
      );
    state.nextAt = new Date(Date.now() + 37000).toISOString();
    state.lastRun.counts.applied++;
    state.ledger.unshift({ id: '4031772210', status: 'applied', job: 'Desenvolvedor Full Stack .NET | Arquipélago Tech', at,
      trail: state.log.filter(l => l.jobId === '4031772210'), sent: [['Anos de experiência com C#', '4'], ['Disponibilidade para início', 'Imediata']] });
    state.currentJob = null;
    push();
  }, 2500);
})();
